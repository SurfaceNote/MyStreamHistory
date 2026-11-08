namespace MyStreamHistory.TwitchTrackingService.Domain.Entities;

/// <summary>
/// One row per channel, UTC day and activity kind (registered, visit, overlay, panel).
/// Used for product metrics such as weekly engaged channels; contains no viewer data.
/// </summary>
public class ChannelActivityDay
{
    public int TwitchUserId { get; set; }
    public DateOnly Day { get; set; }
    public string Kind { get; set; } = null!;
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}
