namespace MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;

public class GetStreamerStatisticsResponseContract
{
    public int TotalStreamsCount { get; set; }
    public int TotalUniqueGamesCount { get; set; }
    public double TotalStreamedHours { get; set; }
    public List<CategoryStatisticsDto> Categories { get; set; } = new();
}

