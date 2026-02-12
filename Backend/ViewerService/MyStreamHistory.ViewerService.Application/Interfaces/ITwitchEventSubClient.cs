namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface ITwitchEventSubClient
{
    Task<string> SubscribeToChatMessagesAsync(string broadcasterId, string accessToken, CancellationToken cancellationToken = default);
    Task<List<EventSubSubscription>> GetSubscriptionsAsync(string? type = null, CancellationToken cancellationToken = default);
    Task UnsubscribeAsync(string subscriptionId, string accessToken, CancellationToken cancellationToken = default);
    Task<int> CleanupAllChatSubscriptionsAsync(CancellationToken cancellationToken = default);
}

public class EventSubSubscription
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public EventSubCondition? Condition { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

public class EventSubCondition
{
    public string? BroadcasterUserId { get; set; }
    public string? UserId { get; set; }
}

