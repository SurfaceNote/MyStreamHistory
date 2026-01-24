namespace MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;

public class StreamOfflineEventContract
{
    public int BroadcasterUserId { get; set; }
    public string BroadcasterUserLogin { get; set; } = null!;
    public string BroadcasterUserName { get; set; } = null!;
}

