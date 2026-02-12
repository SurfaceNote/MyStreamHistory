namespace MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;

public class StreamOfflineEventContract
{
    public string MessageId { get; set; } = string.Empty;
    public int BroadcasterUserId { get; set; }
    public string BroadcasterUserLogin { get; set; } = null!;
    public string BroadcasterUserName { get; set; } = null!;
}

