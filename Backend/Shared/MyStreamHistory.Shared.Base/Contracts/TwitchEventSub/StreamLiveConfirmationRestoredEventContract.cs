namespace MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;

public class StreamLiveConfirmationRestoredEventContract
{
    public Guid StreamSessionId { get; set; }
    public int BroadcasterUserId { get; set; }
    public DateTime ConfirmedAt { get; set; }
}
