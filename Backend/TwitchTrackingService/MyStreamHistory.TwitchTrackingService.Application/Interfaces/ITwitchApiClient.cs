using MyStreamHistory.TwitchTrackingService.Application.DTOs;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface ITwitchApiClient
{
    Task<string?> CreateEventSubSubscriptionAsync(int broadcasterUserId, string eventType, CancellationToken cancellationToken = default);
    Task<List<int>> GetExistingSubscriptionsAsync(CancellationToken cancellationToken = default);
    Task<EventSubSubscriptionsDto> GetEventSubSubscriptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>All EventSub subscriptions of the app across pages; null when Twitch could not be read.</summary>
    Task<List<EventSubSubscriptionDto>?> GetAllEventSubSubscriptionsAsync(CancellationToken cancellationToken = default);
    Task DeleteSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task<int> DeleteAllSubscriptionsAsync(CancellationToken cancellationToken = default);
    Task<(int successCount, int failCount)> SubscribeToAllUsersAsync(List<int> userIds, CancellationToken cancellationToken = default);
    Task<List<TwitchStreamDto>> GetStreamsAsync(List<int> userIds, CancellationToken cancellationToken = default);
    Task<List<TwitchGameDataDto>> GetGamesAsync(List<string> gameIds, CancellationToken cancellationToken = default);
}

