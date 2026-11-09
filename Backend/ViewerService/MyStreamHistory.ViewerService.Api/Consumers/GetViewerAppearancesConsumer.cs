using MassTransit;
using Microsoft.EntityFrameworkCore;
using MyStreamHistory.Shared.Base.Contracts.Viewers;
using MyStreamHistory.ViewerService.Infrastructure.Persistence;

namespace MyStreamHistory.ViewerService.Api.Consumers;

/// <summary>
/// A chat account appeared on a stream when it has category watch data or a presence minute there,
/// the same sources the stream page uses for its chat account list.
/// </summary>
public class GetViewerAppearancesConsumer(
    ViewerServiceDbContext db,
    ILogger<GetViewerAppearancesConsumer> logger) : IConsumer<GetViewerAppearancesRequest>
{
    public async Task Consume(ConsumeContext<GetViewerAppearancesRequest> context)
    {
        var request = context.Message;
        var cancellationToken = context.CancellationToken;

        try
        {
            var sessionByCategory = request.Sessions
                .SelectMany(session => session.CategoryIds.Select(categoryId => (categoryId, session.Id)))
                .GroupBy(item => item.categoryId)
                .ToDictionary(group => group.Key, group => group.First().Id);
            var sessionIds = request.Sessions.Select(session => session.Id).Distinct().ToList();
            var viewerIds = request.ViewerTwitchUserIds.Distinct().ToList();

            var appearances = await LoadAppearancesAsync(
                request.StreamerTwitchUserId, viewerIds, sessionIds, sessionByCategory, cancellationToken);

            var countSessionIds = request.CountSessionIds.Distinct().ToList();
            var counts = new List<SessionAudienceCountDto>();
            if (countSessionIds.Count > 0)
            {
                var countSessionSet = countSessionIds.ToHashSet();
                var countCategories = sessionByCategory
                    .Where(pair => countSessionSet.Contains(pair.Value))
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                var audience = await LoadAppearancesAsync(
                    request.StreamerTwitchUserId, null, countSessionIds, countCategories, cancellationToken);
                var excluded = await LoadExcludedViewerIdsAsync(audience.Select(item => item.ViewerId).Distinct().ToList(), cancellationToken);
                excluded.Add(request.StreamerTwitchUserId);

                counts = countSessionIds.Select(sessionId => new SessionAudienceCountDto
                {
                    SessionId = sessionId,
                    Count = audience
                        .Where(item => item.SessionId == sessionId && !excluded.Contains(item.ViewerId))
                        .Select(item => item.ViewerId)
                        .Distinct()
                        .Count()
                }).ToList();
            }

            await context.RespondAsync(new GetViewerAppearancesResponse
            {
                Success = true,
                Viewers = appearances
                    .GroupBy(item => item.ViewerId)
                    .Select(group => new ViewerAppearanceDto
                    {
                        ViewerTwitchUserId = group.Key,
                        SessionIds = group.Select(item => item.SessionId).Distinct().ToList()
                    })
                    .ToList(),
                SessionAudienceCounts = counts
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load viewer appearances for streamer {StreamerTwitchUserId}", request.StreamerTwitchUserId);
            await context.RespondAsync(new GetViewerAppearancesResponse { Success = false, Error = ex.Message });
        }
    }

    private async Task<List<(string ViewerId, Guid SessionId)>> LoadAppearancesAsync(
        string streamerTwitchUserId,
        List<string>? viewerIds,
        List<Guid> sessionIds,
        Dictionary<Guid, Guid> sessionByCategory,
        CancellationToken cancellationToken)
    {
        var result = new List<(string ViewerId, Guid SessionId)>();
        if (sessionIds.Count == 0 || viewerIds is { Count: 0 })
        {
            return result;
        }

        var categoryIds = sessionByCategory.Keys.ToList();
        if (categoryIds.Count > 0)
        {
            var watches = db.ViewerCategoryWatches.AsNoTracking().Where(w => categoryIds.Contains(w.StreamCategoryId));
            if (viewerIds != null) watches = watches.Where(w => viewerIds.Contains(w.Viewer.TwitchUserId));
            var rows = await watches
                .Select(w => new { w.Viewer.TwitchUserId, w.StreamCategoryId })
                .Distinct()
                .ToListAsync(cancellationToken);
            result.AddRange(rows.Select(row => (row.TwitchUserId, sessionByCategory[row.StreamCategoryId])));
        }

        var presences = db.CommunityPresences.AsNoTracking()
            .Where(p => p.StreamerTwitchUserId == streamerTwitchUserId && sessionIds.Contains(p.StreamSessionId));
        if (viewerIds != null) presences = presences.Where(p => viewerIds.Contains(p.ViewerTwitchUserId));
        var presenceRows = await presences
            .Select(p => new { p.ViewerTwitchUserId, p.StreamSessionId })
            .Distinct()
            .ToListAsync(cancellationToken);
        result.AddRange(presenceRows.Select(row => (row.ViewerTwitchUserId, row.StreamSessionId)));

        return result;
    }

    private async Task<HashSet<string>> LoadExcludedViewerIdsAsync(List<string> viewerIds, CancellationToken cancellationToken)
    {
        var excludedLogins = await db.RankingExcludedAccounts.AsNoTracking().Select(a => a.Login).ToListAsync(cancellationToken);
        if (excludedLogins.Count == 0 || viewerIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var ids = await db.Viewers.AsNoTracking()
            .Where(v => viewerIds.Contains(v.TwitchUserId) && excludedLogins.Contains(v.Login.ToLower()))
            .Select(v => v.TwitchUserId)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet(StringComparer.Ordinal);
    }
}
