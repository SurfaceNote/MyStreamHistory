namespace MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;

public class ChatMessageEventContract
{
    public string MessageId { get; set; } = string.Empty;
    public string BroadcasterUserId { get; set; } = string.Empty;
    public string BroadcasterUserLogin { get; set; } = string.Empty;
    public string BroadcasterUserName { get; set; } = string.Empty;
    public string ChatterUserId { get; set; } = string.Empty;
    public string ChatterUserLogin { get; set; } = string.Empty;
    public string ChatterUserName { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public int CharacterCount { get; set; }
    public DateTime MessageTimestamp { get; set; }
}

