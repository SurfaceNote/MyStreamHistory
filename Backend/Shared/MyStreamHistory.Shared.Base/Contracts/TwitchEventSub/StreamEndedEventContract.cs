namespace MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;

/// <summary>
/// Stream ended event, published by TwitchTrackingService after processing StreamOfflineEventContract.
/// This event ensures that the stream was properly ended in TwitchTrackingService.
/// </summary>
public class StreamEndedEventContract
{
    public Guid StreamSessionId { get; set; }
    public int BroadcasterUserId { get; set; }
    public string BroadcasterUserLogin { get; set; } = null!;
    public string BroadcasterUserName { get; set; } = null!;
    public DateTime EndedAt { get; set; }
}

