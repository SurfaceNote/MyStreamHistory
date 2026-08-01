namespace MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;

public class StreamLiveConfirmationLostEventContract
{
    public Guid StreamSessionId { get; set; }
    public int BroadcasterUserId { get; set; }
    public DateTime MissingSince { get; set; }
}
