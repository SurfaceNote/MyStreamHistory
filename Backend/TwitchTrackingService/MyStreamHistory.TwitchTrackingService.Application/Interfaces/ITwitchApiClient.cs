using MyStreamHistory.TwitchTrackingService.Application.DTOs;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface ITwitchApiClient
{
    Task<string?> CreateEventSubSubscriptionAsync(int broadcasterUserId, string eventType, CancellationToken cancellationToken = default);
    Task<List<int>> GetExistingSubscriptionsAsync(CancellationToken cancellationToken = default);
    Task DeleteSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task<List<TwitchStreamDto>> GetStreamsAsync(List<int> userIds, CancellationToken cancellationToken = default);
}

