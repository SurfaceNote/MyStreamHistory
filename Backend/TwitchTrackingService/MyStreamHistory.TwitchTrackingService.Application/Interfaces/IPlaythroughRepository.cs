using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface IPlaythroughRepository
{
    Task<Playthrough?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Playthrough>> GetByTwitchUserIdAsync(int twitchUserId, CancellationToken cancellationToken = default);
    Task<Guid?> GetAutoAddPlaythroughIdAsync(int twitchUserId, Guid twitchCategoryId, CancellationToken cancellationToken = default);
    Task<bool> HasConflictingAutoAddAsync(int twitchUserId, Guid twitchCategoryId, Guid? excludedPlaythroughId, CancellationToken cancellationToken = default);
    Task AddAsync(Playthrough playthrough, CancellationToken cancellationToken = default);
    Task UpdateAsync(Playthrough playthrough, CancellationToken cancellationToken = default);
    Task DeleteAsync(Playthrough playthrough, CancellationToken cancellationToken = default);
    IQueryable<Playthrough> Query();
}
