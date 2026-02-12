namespace MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;

/// <summary>
/// Stream created event, published by TwitchTrackingService after processing StreamOnlineEventContract.
/// This event ensures that the stream was properly created in TwitchTrackingService.
/// </summary>
public class StreamCreatedEventContract
{
    public Guid StreamSessionId { get; set; }
    public int BroadcasterUserId { get; set; }
    public string BroadcasterUserLogin { get; set; } = null!;
    public string BroadcasterUserName { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public string Type { get; set; } = null!;
}

