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
            if (request.Level != null && request.Level is not ("core" or "regular" or "returning" or "occasional" or "inactive" or "unknown" or "declining"))
                return new() { Error = "invalidLevel" };
            string? after = null;
            var cursorVersion = settings.Revision + "-" + settings.CalculatedAt?.Ticks;
            if (request.Cursor != null)
            {
                try
                {
                    var cursor = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(request.Cursor)).Split(':', 2);
                    if (cursor.Length != 2 || cursor[0] != cursorVersion) return new() { Error = "staleCursor" };
                    after = cursor[1];
                }
                catch (FormatException) { return new() { Error = "invalidCursor" }; }
            }
            var members = JsonSerializer.Deserialize<List<CommunityMember>>(settings.MembersJson, Json)!;
            var page = members.Where(m => request.Level == null || request.Level == "declining" && m.Declining || m.Level == request.Level)
                .Where(m => after == null || string.CompareOrdinal(m.TwitchUserId, after) > 0)
                .OrderBy(m => m.TwitchUserId, StringComparer.Ordinal).Take(51).ToList();
            response.Members = page.Take(50).ToList();
            if (page.Count > 50)
                response.NextCursor = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(cursorVersion + ":" + page[49].TwitchUserId));
        }
        if (request.Operation == "exclusions")
        {
            var excluded = await db.CommunityExclusions.Where(x => x.StreamerTwitchUserId == channel).Select(x => x.ViewerTwitchUserId).ToListAsync(ct);
            var profiles = await db.Viewers.Where(x => excluded.Contains(x.TwitchUserId)).ToDictionaryAsync(x => x.TwitchUserId, ct);
            response.Exclusions = excluded.Select(id => new CommunityExcludedAccount { TwitchUserId = id, DisplayName = profiles.GetValueOrDefault(id)?.DisplayName ?? id }).ToList();
        }
        return response;
    }

    public async Task RecalculatePendingAsync(CancellationToken ct)
    {
        if (!Enabled) return;
        var before = DateTime.UtcNow.AddHours(-1);
        var channels = await db.CommunitySettings.AsNoTracking().Where(s => s.Pending || s.CalculatedAt == null || s.CalculatedAt < before)
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

    private async Task RecalculateAsync(string channel, CancellationToken ct)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var settings = await db.CommunitySettings.AsNoTracking().SingleAsync(s => s.StreamerTwitchUserId == channel, ct);
        var metadata = await bus.SendRequestAsync<GetCommunitySessionsRequest, GetCommunitySessionsResponse>(new() { TwitchUserId = int.Parse(channel) }, ct);
        var sessions = metadata.Success?.Sessions ?? throw new InvalidOperationException("Session metadata unavailable");
        var categories = sessions.SelectMany(s => s.CategoryIds).ToArray();
        var watches = await db.ViewerCategoryWatches.AsNoTracking().Where(w => categories.Contains(w.StreamCategoryId)).ToListAsync(ct);
        var viewerIds = watches.Select(w => w.ViewerId).Distinct().ToArray();
        var presence = await db.CommunityPresences.AsNoTracking().Where(x => x.StreamerTwitchUserId == channel).ToListAsync(ct);
        var messages = await db.CommunityMessageAggregates.AsNoTracking().Where(x => x.StreamerTwitchUserId == channel).ToListAsync(ct);
        var userIds = presence.Select(p => p.ViewerTwitchUserId).Concat(messages.Select(m => m.ViewerTwitchUserId)).Distinct().ToArray();
        var profiles = await db.Viewers.AsNoTracking().Where(v => viewerIds.Contains(v.Id) || userIds.Contains(v.TwitchUserId)).ToListAsync(ct);
        var knownCore = await db.CommunityDailyStates.Where(s => s.StreamerTwitchUserId == channel && s.Revision == settings.Revision && s.Level == "core")
            .GroupBy(s => s.ViewerTwitchUserId).Select(g => new { User = g.Key, Date = g.Min(s => s.Date) }).ToDictionaryAsync(x => x.User, x => x.Date, ct);
        var result = CommunityCalculator.Calculate(new()
        {
            Now = DateTime.UtcNow, TimeZoneId = settings.TimeZoneId, TrackingStartedAt = settings.TrackingStartedAt,
            Sessions = sessions, Presence = presence, Messages = messages, HistoricalWatches = watches, Viewers = profiles,
            Polls = await db.CommunityPolls.AsNoTracking().Where(x => x.StreamerTwitchUserId == channel).ToListAsync(ct),
            Exclusions = (await db.CommunityExclusions.Where(x => x.StreamerTwitchUserId == channel).Select(x => x.ViewerTwitchUserId).ToListAsync(ct)).ToHashSet(),
            KnownCoreDates = knownCore
        });
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Lock only the publication stage; leave the previous completed report readable during calculation.
        var latest = await db.CommunitySettings.FromSqlInterpolated($"""SELECT * FROM "CommunitySettings" WHERE "StreamerTwitchUserId" = {channel} FOR UPDATE""").AsNoTracking().SingleAsync(ct);
        if (latest.Revision != settings.Revision) return;
        var first = result.Report.AsOf!.Value.AddDays(-27);
        await db.CommunityDailyStates.Where(s => s.StreamerTwitchUserId == channel && s.Revision == settings.Revision
            && s.MethodVersion == CommunityReport.CurrentMethodVersion && s.Date >= first).ExecuteDeleteAsync(ct);
        db.CommunityDailyStates.AddRange(result.DailyStates.Select(s => new CommunityDailyState
        {
            StreamerTwitchUserId = channel, Revision = settings.Revision, MethodVersion = CommunityReport.CurrentMethodVersion,
            Date = s.Date, ViewerTwitchUserId = s.Member.TwitchUserId, Level = s.Member.Level, Quality = s.Member.Quality,
            Transition = s.Member.Returned && s.Member.Transition is not (null or "returned") ? s.Member.Transition + ",returned" : s.Member.Transition
        }));
        await db.SaveChangesAsync(ct);
        var reportJson = JsonSerializer.Serialize(result.Report, Json);
        var membersJson = JsonSerializer.Serialize(result.Members, Json);
        await db.CommunitySettings.Where(s => s.StreamerTwitchUserId == channel && s.Revision == settings.Revision)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReportJson, reportJson).SetProperty(x => x.MembersJson, membersJson)
                .SetProperty(x => x.Pending, false).SetProperty(x => x.CalculatedAt, DateTime.UtcNow), ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        logger.LogInformation("Community report channel {Channel}: quality={Quality}, eligibleDays={Days}, durationMs={Duration}",
            channel, result.Report.Quality, result.Report.EligibleDays, started.ElapsedMilliseconds);
    }
}
