using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface IViewerRepository
{
    Task<Viewer?> GetByTwitchUserIdAsync(string twitchUserId, CancellationToken cancellationToken = default);
    Task<Viewer> AddAsync(Viewer viewer, CancellationToken cancellationToken = default);
    Task<List<Viewer>> GetOrCreateViewersAsync(List<(string TwitchUserId, string Login, string DisplayName)> viewers, CancellationToken cancellationToken = default);
    Task UpdateAsync(Viewer viewer, CancellationToken cancellationToken = default);
    Task BulkUpdateAsync(List<Viewer> viewers, CancellationToken cancellationToken = default);
}

