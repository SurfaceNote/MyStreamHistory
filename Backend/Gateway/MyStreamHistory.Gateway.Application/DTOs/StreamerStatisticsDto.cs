namespace MyStreamHistory.Gateway.Application.DTOs;

public class StreamerStatisticsDto
{
    public int TotalStreamsCount { get; set; }
    public int TotalUniqueGamesCount { get; set; }
    public double TotalStreamedHours { get; set; }
    public double AllGamesTotalHours { get; set; }
    public List<CategoryStatisticsDto> Categories { get; set; } = new();
    public List<PlaythroughStatisticsDto> Playthroughs { get; set; } = new();
    public StreamerDashboardDto Dashboard { get; set; } = new();
}

