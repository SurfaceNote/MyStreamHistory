namespace MyStreamHistory.ViewerService.Domain.Entities;

public class ViewerStats
{
    public Guid Id { get; set; }
    public Guid ViewerId { get; set; }
    public string StreamerTwitchUserId { get; set; } = string.Empty;
    public int MinutesWatched { get; set; }
    public decimal EarnedMsgPoints { get; set; }
    public decimal Experience { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    
    // Navigation properties
    public Viewer Viewer { get; set; } = null!;
}

