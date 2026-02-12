namespace MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;

/// <summary>
/// Raw channel.update event from Twitch EventSub.
/// Published by Gateway, processed by TwitchTrackingService.
/// TwitchTrackingService then publishes StreamCategoryChangedEventContract for other services.
/// </summary>
public class ChannelUpdateEventContract
{
    public string MessageId { get; set; } = string.Empty;
    public int BroadcasterUserId { get; set; }
    public string BroadcasterUserLogin { get; set; } = null!;
    public string BroadcasterUserName { get; set; } = null!;
    public string StreamTitle { get; set; } = null!;
    public string Language { get; set; } = null!;
    public string CategoryId { get; set; } = null!;
    public string CategoryName { get; set; } = null!;
}

