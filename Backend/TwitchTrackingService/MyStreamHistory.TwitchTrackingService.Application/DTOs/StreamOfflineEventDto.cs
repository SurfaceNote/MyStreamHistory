namespace MyStreamHistory.TwitchTrackingService.Application.DTOs;

public class StreamOfflineEventDto
{
    public int BroadcasterUserId { get; set; }
    public string BroadcasterUserLogin { get; set; } = null!;
    public string BroadcasterUserName { get; set; } = null!;
}

