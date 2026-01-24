namespace MyStreamHistory.TwitchTrackingService.Application.DTOs;

public class StreamOnlineEventDto
{
    public int BroadcasterUserId { get; set; }
    public string BroadcasterUserLogin { get; set; } = null!;
    public string BroadcasterUserName { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public string Type { get; set; } = null!; // "live", "playlist", "watch_party", "premiere", "rerun"
}

