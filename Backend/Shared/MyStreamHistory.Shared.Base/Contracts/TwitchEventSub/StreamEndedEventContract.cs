namespace MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;

/// <summary>
/// Stream ended event, published after EventSub offline processing or polling reconciliation.
/// This event confirms that TwitchTrackingService atomically ended the local stream session.
/// </summary>
public class StreamEndedEventContract
{
    public Guid StreamSessionId { get; set; }
    public int BroadcasterUserId { get; set; }
    public string BroadcasterUserLogin { get; set; } = null!;
    public string BroadcasterUserName { get; set; } = null!;
    public DateTime EndedAt { get; set; }
}

