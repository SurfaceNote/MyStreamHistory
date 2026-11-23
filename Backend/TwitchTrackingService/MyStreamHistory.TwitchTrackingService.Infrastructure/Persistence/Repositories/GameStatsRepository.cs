using Microsoft.EntityFrameworkCore;
using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Repositories;

public class GameStatsRepository(TwitchTrackingDbContext context) : IGameStatsRepository
{
    public async Task<IReadOnlyList<GameSegmentRow>> GetSegmentsAsync(
        string? categoryTwitchId,
        CancellationToken cancellationToken = default)
    {
        var query = context.Set<Domain.Entities.StreamCategory>().AsNoTracking();
        query = categoryTwitchId == null
            ? query.Where(sc => sc.TwitchCategory.IgdbId != null && sc.TwitchCategory.IgdbId != "")
            : query.Where(sc => sc.TwitchCategory.TwitchId == categoryTwitchId);

        return await query
            .Select(sc => new GameSegmentRow(
                sc.TwitchCategoryId,
                sc.TwitchCategory.TwitchId,
                sc.TwitchCategory.Name,
                sc.TwitchCategory.BoxArtUrl,
                sc.TwitchCategory.IgdbId,
                sc.StreamSessionId,
                sc.StreamSession.TwitchUserId,
                sc.StreamSession.StreamerLogin,
                sc.StreamSession.StreamerDisplayName,
                sc.StreamSession.StreamerAvatarUrl,
                sc.StreamSession.Language,
                sc.StreamSession.IsLive,
                sc.StreamSession.LastConfirmedLiveAt,
                sc.StreamSession.StartedAt,
                sc.StreamSession.EndedAt,
                sc.StartedAt,
                sc.EndedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GamePlaythroughRow>> GetPlaythroughsAsync(
        IReadOnlyCollection<Guid> twitchCategoryIds,
        CancellationToken cancellationToken = default)
    {
        return await context.Set<Domain.Entities.Playthrough>()
            .AsNoTracking()
            .Where(p => twitchCategoryIds.Contains(p.TwitchCategoryId))
            .Select(p => new GamePlaythroughRow(
                p.TwitchCategoryId,
                p.TwitchUserId,
                p.Status,
                p.Title,
                p.UpdatedAt,
                p.PlaythroughStreamCategories
                    .Select(psc => psc.StreamCategory.StreamSessionId)
                    .Distinct()
                    .Count()))
            .ToListAsync(cancellationToken);
    }
}
