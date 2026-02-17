namespace MyStreamHistory.Gateway.Application.DTOs;

public class StreamerStatisticsDto
{
    public int TotalStreamsCount { get; set; }
    public int TotalUniqueGamesCount { get; set; }
    public double TotalStreamedHours { get; set; }
    public List<CategoryStatisticsDto> Categories { get; set; } = new();
}

