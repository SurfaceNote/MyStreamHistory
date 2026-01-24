namespace MyStreamHistory.TwitchTrackingService.Domain.Entities;

public class StreamSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TwitchUserId { get; set; }
    public string StreamerLogin { get; set; } = null!;
    public string StreamerDisplayName { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsLive { get; set; }
    public string? StreamTitle { get; set; }
    public string? GameName { get; set; }
    public int? ViewerCount { get; set; }
}

