using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface IEventSubSubscriptionRepository
{
    Task<EventSubSubscription> AddAsync(EventSubSubscription entity, CancellationToken cancellationToken = default);
    Task<EventSubSubscription> UpdateAsync(EventSubSubscription entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(EventSubSubscription entity, CancellationToken cancellationToken = default);
    Task<EventSubSubscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<EventSubSubscription>> GetAllAsync(CancellationToken cancellationToken = default);
    IQueryable<EventSubSubscription> Query();
}

