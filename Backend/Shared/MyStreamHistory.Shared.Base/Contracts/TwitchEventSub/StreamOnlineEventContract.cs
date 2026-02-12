namespace MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;

public class StreamOnlineEventContract
{
    public string MessageId { get; set; } = string.Empty;
    public int BroadcasterUserId { get; set; }
    public string BroadcasterUserLogin { get; set; } = null!;
    public string BroadcasterUserName { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public string Type { get; set; } = null!;
}

