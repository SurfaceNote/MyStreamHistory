namespace MyStreamHistory.ViewerService.Application.DTOs;

public class ViewerStatsDto
{
    public Guid Id { get; set; }
    public Guid ViewerId { get; set; }
    public string StreamerTwitchUserId { get; set; } = string.Empty;
    public int MinutesWatched { get; set; }
    public decimal EarnedMsgPoints { get; set; }
    public decimal Experience { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}

