namespace MyStreamHistory.Shared.Base.Contracts.Viewers;

public class ViewerStreamHistoryDto
{
    public Guid StreamSessionId { get; set; }
    public string? StreamTitle { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int DurationMinutes { get; set; }
    public int MinutesWatched { get; set; }
    public decimal WatchedPercentage { get; set; }
    public decimal ChatPoints { get; set; }
    public List<string> Categories { get; set; } = new();
}
