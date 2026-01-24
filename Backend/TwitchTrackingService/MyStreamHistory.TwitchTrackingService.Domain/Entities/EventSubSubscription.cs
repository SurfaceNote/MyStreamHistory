namespace MyStreamHistory.TwitchTrackingService.Domain.Entities;

public class EventSubSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TwitchSubscriptionId { get; set; } = null!;
    public int TwitchUserId { get; set; }
    public string EventType { get; set; } = null!; // stream.online, stream.offline
    public string Status { get; set; } = null!; // enabled, disabled, webhook_callback_verification_pending
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

