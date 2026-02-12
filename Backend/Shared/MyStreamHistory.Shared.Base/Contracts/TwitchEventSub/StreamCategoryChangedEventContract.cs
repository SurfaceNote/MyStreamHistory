namespace MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;

public class StreamCategoryChangedEventContract
{
    public string MessageId { get; set; } = string.Empty;
    public Guid StreamSessionId { get; set; }
    public int BroadcasterUserId { get; set; }
    public Guid? OldCategoryId { get; set; }
    public Guid NewCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}

