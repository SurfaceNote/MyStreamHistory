using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.Community;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Application.Services;
using MyStreamHistory.ViewerService.Domain.Entities;
using MyStreamHistory.ViewerService.Infrastructure.Persistence;
using MyStreamHistory.ViewerService.Infrastructure.Options;

namespace MyStreamHistory.ViewerService.Infrastructure.Services;

public class CommunityService(ViewerServiceDbContext db, ITransportBus bus, IConfiguration configuration, IHostEnvironment environment,
    ILogger<CommunityService> logger, IOptions<CommunityAnalyticsOptions> options) : ICommunityService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private bool Enabled => configuration.GetValue<bool>("Features:CommunityAnalytics:Enabled");
    private TimeSpan MessageReceiptRetention => TimeSpan.FromDays(options.Value.MessageReceiptRetentionDays);
    private const int PageSize = 50;
    /// <summary>No roster poll for this long means the broadcast is over.</summary>
    private static readonly TimeSpan BroadcastEndDelay = TimeSpan.FromMinutes(10);
    /// <summary>Late chat messages and the end-of-stream update settle before a session is rolled up.</summary>
    private static readonly TimeSpan SessionSettleDelay = TimeSpan.FromMinutes(5);

    private async Task EnsureSettings(string channel, DateTime? trackingAt, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CommunitySettings" ("StreamerTwitchUserId", "TimeZoneId", "TimeZoneConfigured", "Revision", "Pending", "TrackingStartedAt")
            VALUES ({channel}, 'UTC', FALSE, 0, TRUE, {trackingAt})
            ON CONFLICT ("StreamerTwitchUserId") DO NOTHING
            """, ct);
        if (trackingAt.HasValue)
            await db.CommunitySettings.Where(s => s.StreamerTwitchUserId == channel && s.TrackingStartedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.TrackingStartedAt, trackingAt), ct);
    }

    public async Task RecordPollAsync(string channel, Guid session, DateTime observedAt,
        IReadOnlyCollection<TwitchChatterDto>? chatters, CancellationToken ct)
    {
        if (!Enabled) return;
        var minute = CommunityCalculator.FloorMinute(observedAt);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await EnsureSettings(channel, observedAt, ct);
        var complete = chatters != null;
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CommunityPolls" ("StreamerTwitchUserId", "Minute", "StreamSessionId", "ObservedAt", "Complete")
            VALUES ({channel}, {minute}, {session}, {observedAt}, {complete})
            ON CONFLICT ("StreamerTwitchUserId", "Minute") DO UPDATE
              SET "Complete" = EXCLUDED."Complete", "ObservedAt" = EXCLUDED."ObservedAt", "StreamSessionId" = EXCLUDED."StreamSessionId"
              WHERE NOT "CommunityPolls"."Complete" AND EXCLUDED."Complete"
            """, ct);
        if (changed > 0 && chatters is { Count: > 0 })
        {
            var users = chatters.Select(c => c.UserId).Distinct().ToArray();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "CommunityPresences" ("StreamerTwitchUserId", "ViewerTwitchUserId", "StreamSessionId", "Minute")
                SELECT {channel}, u, {session}, {minute} FROM unnest({users}) AS u
                ON CONFLICT DO NOTHING
                """, ct);
        }
        // Polling stops when the broadcast ends; the worker rebuilds the report shortly after that.
        await db.CommunitySettings.Where(s => s.StreamerTwitchUserId == channel && (s.LastActivityAt == null || s.LastActivityAt < observedAt))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastActivityAt, observedAt), ct);
        await tx.CommitAsync(ct);
        logger.LogInformation("Community poll channel {Channel}: complete={Complete}, accounts={Count}", channel, complete, chatters?.Count);
    }

    public async Task RecordMessageAsync(ChatMessageEventContract message, Guid session, CancellationToken ct)
    {
        if (!Enabled || message.SourceBroadcasterUserId != null && message.SourceBroadcasterUserId != message.BroadcasterUserId) return;
        var received = DateTime.SpecifyKind(message.MessageTimestamp, DateTimeKind.Utc);
        // Once the deduplication key expires, an old replay must not increment the aggregate again.
        if (received < DateTime.UtcNow - MessageReceiptRetention)
        {
            logger.LogWarning("Ignoring chat message outside the retention window for channel {Channel}, received at {ReceivedAt}",
                message.BroadcasterUserId, received);
            return;
        }
        var channel = message.BroadcasterUserId;
        var id = message.TwitchMessageId ?? "legacy:" + message.MessageId;
        var provisional = message.TwitchMessageId == null || !message.HasSourceMetadata;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await EnsureSettings(channel, null, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Viewers" ("Id", "TwitchUserId", "DisplayName", "Login", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {message.ChatterUserId}, {message.ChatterUserName}, {message.ChatterUserLogin}, {received})
            ON CONFLICT ("TwitchUserId") DO NOTHING
            """, ct);
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CommunityMessageReceipts" ("StreamerTwitchUserId", "MessageId", "ReceivedAt")
            VALUES ({channel}, {id}, {received}) ON CONFLICT DO NOTHING
            """, ct);
        if (inserted > 0)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "CommunityMessageAggregates" ("StreamerTwitchUserId", "StreamSessionId", "ViewerTwitchUserId", "Count", "Provisional")
                VALUES ({channel}, {session}, {message.ChatterUserId}, 1, {provisional})
                ON CONFLICT ("StreamerTwitchUserId", "StreamSessionId", "ViewerTwitchUserId") DO UPDATE
                SET "Count" = "CommunityMessageAggregates"."Count" + 1,
                    "Provisional" = "CommunityMessageAggregates"."Provisional" OR EXCLUDED."Provisional"
                """, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<int> DeleteExpiredMessageReceiptsAsync(CancellationToken ct)
    {
        var threshold = DateTime.UtcNow - MessageReceiptRetention;
        const int batchSize = 1000;
        var total = 0;
        int deleted;
        do
        {
            // Commit each batch separately; do not load the journal or hold a transaction over the whole backlog.
            deleted = await db.Database.ExecuteSqlInterpolatedAsync($"""
                DELETE FROM "CommunityMessageReceipts"
                WHERE ("StreamerTwitchUserId", "MessageId") IN (
                    SELECT "StreamerTwitchUserId", "MessageId" FROM "CommunityMessageReceipts"
                    WHERE "ReceivedAt" < {threshold}
                    ORDER BY "ReceivedAt"
                    LIMIT {batchSize}
                )
                """, ct);
            total += deleted;
        } while (deleted == batchSize);
        return total;
    }

    public async Task<CommunityResponse> HandleAsync(CommunityRequest request, CancellationToken ct)
    {
        if (request.Operation == "overview") return await OverviewAsync(ct);
        var channel = request.StreamerTwitchUserId;
        if (!int.TryParse(channel, out _)) return new() { Error = "invalidChannel" };
        await EnsureSettings(channel, null, ct);
        var settings = await db.CommunitySettings.SingleAsync(s => s.StreamerTwitchUserId == channel, ct);
        switch (request.Operation)
        {
            case "settings":
                if (string.IsNullOrWhiteSpace(request.TimeZoneId) || request.TimeZoneId.Length > 100
                    || request.TimeZoneId != "UTC" && !TimeZoneInfo.TryConvertIanaIdToWindowsId(request.TimeZoneId, out _))
                    return new() { Error = "invalidTimeZone" };
                try { TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId); }
                catch (TimeZoneNotFoundException) { return new() { Error = "invalidTimeZone" }; }
                await db.CommunitySettings.Where(s => s.StreamerTwitchUserId == channel)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.TimeZoneId, request.TimeZoneId)
                        .SetProperty(x => x.TimeZoneConfigured, true).SetProperty(x => x.Revision, x => x.Revision + 1)
                        .SetProperty(x => x.Pending, true), ct);
                await db.Entry(settings).ReloadAsync(ct);
                break;
            case "exclude":
            case "restore":
                if (string.IsNullOrWhiteSpace(request.ViewerTwitchUserId) || !request.ViewerTwitchUserId.All(char.IsAsciiDigit))
                    return new() { Error = "invalidViewer" };
                await using (var tx = await db.Database.BeginTransactionAsync(ct))
                {
                    if (request.Operation == "exclude")
                        await db.Database.ExecuteSqlInterpolatedAsync($"""
                            INSERT INTO "CommunityExclusions" ("StreamerTwitchUserId", "ViewerTwitchUserId")
                            VALUES ({channel}, {request.ViewerTwitchUserId}) ON CONFLICT DO NOTHING
                            """, ct);
                    else
                        await db.CommunityExclusions.Where(x => x.StreamerTwitchUserId == channel && x.ViewerTwitchUserId == request.ViewerTwitchUserId).ExecuteDeleteAsync(ct);
                    // Atomic increment prevents lost invalidations from concurrent settings changes.
                    await db.CommunitySettings.Where(x => x.StreamerTwitchUserId == channel)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.Revision, x => x.Revision + 1).SetProperty(x => x.Pending, true), ct);
                    await tx.CommitAsync(ct);
                }
                await db.Entry(settings).ReloadAsync(ct);
                break;
        }
        var report = settings.ReportJson == null ? new CommunityReport() : JsonSerializer.Deserialize<CommunityReport>(settings.ReportJson, Json)!;
        report.TimeZoneConfigured = settings.TimeZoneConfigured;
        report.CollectionEnabled = Enabled && (configuration.GetValue<bool?>("Features:TwitchDataCollection:Enabled")
            ?? !environment.IsDevelopment());
        report.Recalculating = settings.Pending && Enabled;
        // Old cached values belong to the old timezone/exclusion revision. Never publish them as current.
        if (settings.Pending)
            report = new() { Recalculating = Enabled, TimeZoneId = settings.TimeZoneId, TimeZoneConfigured = settings.TimeZoneConfigured, CollectionEnabled = report.CollectionEnabled };
        var response = new CommunityResponse { Report = report };
        if (request.Operation == "members" && !settings.Pending && settings.MembersJson != null)
        {
            if (request.Level != null && !CommunitySegments.All.Contains(request.Level)) return new() { Error = "invalidLevel" };
            if (request.Activity != null && request.Activity is not (CommunitySegments.Chatty or CommunitySegments.Silent)) return new() { Error = "invalidActivity" };
            var search = request.Search?.Trim();
            if (search is { Length: > 50 }) return new() { Error = "invalidSearch" };
            var offset = 0;
            var cursorVersion = settings.Revision + "-" + settings.CalculatedAt?.Ticks;
            if (request.Cursor != null)
            {
                try
                {
                    var cursor = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(request.Cursor)).Split(':', 2);
                    if (cursor.Length != 2 || cursor[0] != cursorVersion) return new() { Error = "staleCursor" };
                    if (!int.TryParse(cursor[1], out offset) || offset < 0) return new() { Error = "invalidCursor" };
                }
                catch (FormatException) { return new() { Error = "invalidCursor" }; }
            }
            // Members are stored already ordered by attendance.
            var members = JsonSerializer.Deserialize<List<CommunityMember>>(settings.MembersJson, Json)!
                .Where(m => request.Level == null || m.Segment == request.Level)
                .Where(m => request.Activity == null || m.Activity == request.Activity)
                .Where(m => string.IsNullOrEmpty(search) || m.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || m.Login.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
            response.MembersTotal = members.Count;
            response.Members = members.Skip(offset).Take(PageSize).ToList();
            if (members.Count > offset + PageSize)
                response.NextCursor = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(cursorVersion + ":" + (offset + PageSize)));
        }
        if (request.Operation == "exclusions")
        {
            var excluded = await db.CommunityExclusions.Where(x => x.StreamerTwitchUserId == channel).Select(x => x.ViewerTwitchUserId).ToListAsync(ct);
            var profiles = await db.Viewers.Where(x => excluded.Contains(x.TwitchUserId)).ToDictionaryAsync(x => x.TwitchUserId, ct);
            response.Exclusions = excluded.Select(id => new CommunityExcludedAccount { TwitchUserId = id, DisplayName = profiles.GetValueOrDefault(id)?.DisplayName ?? id }).ToList();
        }
        return response;
    }

    /// <summary>Read-only summary of every channel's published report, for the admin panel. Creates nothing.</summary>
    private async Task<CommunityResponse> OverviewAsync(CancellationToken ct)
    {
        var rows = await db.CommunitySettings.AsNoTracking()
            .Select(s => new { s.StreamerTwitchUserId, s.Pending, s.CalculatedAt, s.LastActivityAt, s.ReportJson })
            .ToListAsync(ct);
        return new CommunityResponse
        {
            Channels = rows.Select(row =>
            {
                var report = row.ReportJson == null ? null : JsonSerializer.Deserialize<CommunityReport>(row.ReportJson, Json);
                // A report from an older method version has a different shape; show it as recalculating.
                if (report?.MethodVersion != CommunityReport.CurrentMethodVersion) report = null;
                return new CommunityChannelSummary
                {
                    TwitchUserId = row.StreamerTwitchUserId, Recalculating = Enabled && (row.Pending || report == null),
                    Quality = report?.Quality ?? "insufficient", TotalBroadcasts = report?.TotalBroadcasts ?? 0,
                    Segments = report?.Segments ?? [], AsOf = report?.AsOf, CalculatedAt = row.CalculatedAt, LastActivityAt = row.LastActivityAt
                };
            }).OrderByDescending(c => c.TotalBroadcasts).ThenBy(c => c.TwitchUserId, StringComparer.Ordinal).ToList()
        };
    }

    public async Task RecalculatePendingAsync(CancellationToken ct)
    {
        if (!Enabled) return;
        var now = DateTime.UtcNow;
        var daily = now.AddHours(-24);
        var ended = now - BroadcastEndDelay;
        // Rebuild on settings changes, once a broadcast has ended (polling went quiet), and daily as a safety net.
        var channels = await db.CommunitySettings.AsNoTracking()
            .Where(s => s.Pending || s.CalculatedAt == null || s.CalculatedAt < daily
                || s.LastActivityAt != null && s.LastActivityAt > s.CalculatedAt && s.LastActivityAt < ended)
            .Select(s => s.StreamerTwitchUserId).ToListAsync(ct);
        foreach (var channel in channels)
        {
            try { await RecalculateAsync(channel, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Community calculation failed for channel {Channel}", channel);
                db.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>Rolls finished sessions up into per-viewer rows once, so reports never re-read raw minutes.</summary>
    private async Task<int> AggregateSessionsAsync(string channel, CommunitySettings settings, IReadOnlyList<CommunitySession> sessions, CancellationToken ct)
    {
        var known = (await db.CommunitySessionSummaries.AsNoTracking().Where(x => x.StreamerTwitchUserId == channel)
            .Select(x => x.StreamSessionId).ToListAsync(ct)).ToHashSet();
        var cutoff = DateTime.UtcNow - SessionSettleDelay;
        var aggregated = 0;
        foreach (var session in sessions.Where(s => !s.IsLive && s.EndedAt.HasValue && s.EndedAt < cutoff && !known.Contains(s.Id)).OrderBy(s => s.StartedAt))
        {
            var startedAt = DateTime.SpecifyKind(session.StartedAt, DateTimeKind.Utc);
            var endedAt = DateTime.SpecifyKind(session.EndedAt!.Value, DateTimeKind.Utc);
            var legacy = settings.TrackingStartedAt == null || endedAt <= settings.TrackingStartedAt;
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var summary = new CommunitySessionSummary
            {
                StreamerTwitchUserId = channel, StreamSessionId = session.Id, StartedAt = startedAt, EndedAt = endedAt,
                Legacy = legacy, AggregatedAt = DateTime.UtcNow
            };
            if (legacy)
            {
                // Before minute polling existed only category totals were stored; they remain estimates.
                var categories = session.CategoryIds.ToArray();
                var watches = await db.ViewerCategoryWatches.AsNoTracking().Where(w => categories.Contains(w.StreamCategoryId))
                    .Join(db.Viewers.AsNoTracking(), w => w.ViewerId, v => v.Id, (w, v) => new { v.TwitchUserId, w.MinutesWatched, w.ChatPoints })
                    .ToListAsync(ct);
                db.CommunitySessionVisits.AddRange(watches.GroupBy(w => w.TwitchUserId).Select(g => new CommunitySessionVisit
                {
                    StreamerTwitchUserId = channel, StreamSessionId = session.Id, ViewerTwitchUserId = g.Key,
                    Minutes = g.Sum(w => w.MinutesWatched), LegacyChat = g.Any(w => w.ChatPoints > 0)
                }));
            }
            else
            {
                var polls = await db.CommunityPolls.AsNoTracking()
                    .Where(p => p.StreamerTwitchUserId == channel && p.StreamSessionId == session.Id && p.Complete)
                    .Select(p => p.Minute).ToListAsync(ct);
                (summary.ExpectedMinutes, summary.CompleteMinutes, summary.LongestGap) = CommunityCalculator.Coverage(startedAt, endedAt, polls);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "CommunitySessionVisits" ("StreamerTwitchUserId", "StreamSessionId", "ViewerTwitchUserId", "Minutes", "LegacyChat")
                    SELECT {channel}, {session.Id}, p."ViewerTwitchUserId", COUNT(*), FALSE
                    FROM "CommunityPresences" p
                    WHERE p."StreamerTwitchUserId" = {channel} AND p."StreamSessionId" = {session.Id}
                    GROUP BY p."ViewerTwitchUserId"
                    ON CONFLICT DO NOTHING
                    """, ct);
            }
            db.CommunitySessionSummaries.Add(summary);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            db.ChangeTracker.Clear();
            aggregated++;
        }
        return aggregated;
    }

    private async Task RecalculateAsync(string channel, CancellationToken ct)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var settings = await db.CommunitySettings.AsNoTracking().SingleAsync(s => s.StreamerTwitchUserId == channel, ct);
        var metadata = await bus.SendRequestAsync<GetCommunitySessionsRequest, GetCommunitySessionsResponse>(new() { TwitchUserId = int.Parse(channel) }, ct);
        var sessions = metadata.Success?.Sessions ?? throw new InvalidOperationException("Session metadata unavailable");
        var aggregated = await AggregateSessionsAsync(channel, settings, sessions, ct);

        var summaries = await db.CommunitySessionSummaries.AsNoTracking().Where(x => x.StreamerTwitchUserId == channel).ToListAsync(ct);
        var visits = await db.CommunitySessionVisits.AsNoTracking().Where(x => x.StreamerTwitchUserId == channel).ToListAsync(ct);
        var messages = await db.CommunityMessageAggregates.AsNoTracking().Where(x => x.StreamerTwitchUserId == channel).ToListAsync(ct);
        var userIds = visits.Select(v => v.ViewerTwitchUserId).Concat(messages.Select(m => m.ViewerTwitchUserId)).Distinct().ToArray();
        var profiles = await db.Viewers.AsNoTracking().Where(v => userIds.Contains(v.TwitchUserId))
            .Select(v => new CommunityProfile(v.TwitchUserId, v.DisplayName, v.Login, v.ProfileImageUrl)).ToListAsync(ct);
        var bots = (await db.RankingExcludedAccounts.AsNoTracking().Select(x => x.Login).ToListAsync(ct)).ToHashSet();
        var exclusions = (await db.CommunityExclusions.AsNoTracking().Where(x => x.StreamerTwitchUserId == channel)
            .Select(x => x.ViewerTwitchUserId).ToListAsync(ct)).ToHashSet();
        var result = CommunityCalculator.Calculate(new()
        {
            Now = DateTime.UtcNow, TimeZoneId = settings.TimeZoneId, StreamerTwitchUserId = channel,
            Sessions = summaries, Visits = visits, Messages = messages, Profiles = profiles, ExcludedLogins = bots, Exclusions = exclusions
        });
        var reportJson = JsonSerializer.Serialize(result.Report, Json);
        var membersJson = JsonSerializer.Serialize(result.Members, Json);
        // Publish only if no settings change arrived during the calculation; otherwise the next run picks it up.
        var published = await db.CommunitySettings.Where(s => s.StreamerTwitchUserId == channel && s.Revision == settings.Revision)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReportJson, reportJson).SetProperty(x => x.MembersJson, membersJson)
                .SetProperty(x => x.Pending, false).SetProperty(x => x.CalculatedAt, DateTime.UtcNow), ct);
        // Bulk updates bypass the change tracker; drop tracked copies so later reads in this scope are fresh.
        db.ChangeTracker.Clear();
        logger.LogInformation("Community report channel {Channel}: quality={Quality}, broadcasts={Broadcasts}, aggregatedSessions={Aggregated}, visits={Visits}, members={Members}, published={Published}, durationMs={Duration}",
            channel, result.Report.Quality, result.Report.TotalBroadcasts, aggregated, visits.Count, result.Members.Count, published > 0, started.ElapsedMilliseconds);
    }
}
