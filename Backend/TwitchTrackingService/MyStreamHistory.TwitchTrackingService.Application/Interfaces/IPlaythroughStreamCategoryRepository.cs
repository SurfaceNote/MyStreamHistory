using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface IPlaythroughStreamCategoryRepository
{
    Task<List<PlaythroughStreamCategory>> GetByPlaythroughIdAsync(Guid playthroughId, CancellationToken cancellationToken = default);
    Task<HashSet<Guid>> GetAssignedStreamCategoryIdsAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid playthroughId, Guid streamCategoryId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(List<PlaythroughStreamCategory> entities, CancellationToken cancellationToken = default);
    Task RemoveRangeAsync(List<PlaythroughStreamCategory> entities, CancellationToken cancellationToken = default);
    IQueryable<PlaythroughStreamCategory> Query();
}
