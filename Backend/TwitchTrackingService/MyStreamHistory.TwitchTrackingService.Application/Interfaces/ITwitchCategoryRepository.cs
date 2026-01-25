using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface ITwitchCategoryRepository
{
    Task<List<TwitchCategory>> GetByTwitchIdsAsync(List<string> twitchIds, CancellationToken cancellationToken = default);
    Task<TwitchCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddRangeAsync(List<TwitchCategory> categories, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(List<TwitchCategory> categories, CancellationToken cancellationToken = default);
}

