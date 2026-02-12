using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface IViewerStatsRepository
{
    Task<ViewerStats?> GetByViewerAndStreamerAsync(Guid viewerId, string streamerTwitchUserId, CancellationToken cancellationToken = default);
    Task<List<ViewerStats>> GetTopViewersByStreamerAsync(string streamerTwitchUserId, int limit = 100, CancellationToken cancellationToken = default);
    Task<List<ViewerStats>> GetByViewerIdAsync(Guid viewerId, CancellationToken cancellationToken = default);
    Task BulkUpsertAsync(List<ViewerStats> stats, CancellationToken cancellationToken = default);
}

