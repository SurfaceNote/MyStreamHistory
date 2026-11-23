using MyStreamHistory.TwitchTrackingService.Application.DTOs;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface IGameStatsRepository
{
    /// <summary>
    /// Category segments of one category (<paramref name="categoryTwitchId"/>), or of every game
    /// (categories with an IGDB id) when it is null.
    /// </summary>
    Task<IReadOnlyList<GameSegmentRow>> GetSegmentsAsync(string? categoryTwitchId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GamePlaythroughRow>> GetPlaythroughsAsync(
        IReadOnlyCollection<Guid> twitchCategoryIds,
        CancellationToken cancellationToken = default);
}
