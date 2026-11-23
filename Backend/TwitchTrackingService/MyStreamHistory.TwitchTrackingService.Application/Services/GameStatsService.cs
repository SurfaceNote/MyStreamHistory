using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Application.Services;

public interface IGameStatsService
{
    /// <summary>Statistics of one Twitch category, or null when nobody on the site has streamed it.</summary>
    Task<GameStats?> GetGameAsync(string categoryTwitchId, CancellationToken cancellationToken = default);

    /// <summary>Summaries of every game streamed on the site; non-game categories are left out.</summary>
    Task<IReadOnlyList<GameSummary>> GetCatalogAsync(CancellationToken cancellationToken = default);
}

public class GameStatsService(IGameStatsRepository repository) : IGameStatsService
{
    public async Task<GameStats?> GetGameAsync(string categoryTwitchId, CancellationToken cancellationToken = default)
    {
        var stats = await BuildAsync(categoryTwitchId, cancellationToken);
        return stats.FirstOrDefault();
    }

    public async Task<IReadOnlyList<GameSummary>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var stats = await BuildAsync(null, cancellationToken);
        return stats.Select(game => game.Summary).Where(summary => summary.IsGame).ToList();
    }

    private async Task<IReadOnlyList<GameStats>> BuildAsync(string? categoryTwitchId, CancellationToken cancellationToken)
    {
        var segments = await repository.GetSegmentsAsync(categoryTwitchId, cancellationToken);
        if (segments.Count == 0)
        {
            return [];
        }

        var categoryIds = segments.Select(s => s.TwitchCategoryId).Distinct().ToList();
        var playthroughs = await repository.GetPlaythroughsAsync(categoryIds, cancellationToken);
        return GameStatsCalculator.Build(segments, playthroughs, DateTime.UtcNow);
    }
}
