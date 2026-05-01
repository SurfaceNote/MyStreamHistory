namespace MyStreamHistory.Shared.Base.Contracts.StreamSessions;

public class StreamerDashboardDto
{
    public string Period { get; set; } = "30d";
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int TotalStreamsCount { get; set; }
    public int TotalUniqueGamesCount { get; set; }
    public double TotalStreamedHours { get; set; }
    public List<TimeSeriesPointDto> StreamedHoursByDay { get; set; } = new();
    public List<TimeSeriesPointDto> StreamedHoursByWeek { get; set; } = new();
    public List<TimeSeriesPointDto> ChatPointsByDay { get; set; } = new();
    public List<TimeSeriesPointDto> ViewerCountByDay { get; set; } = new();
    public List<CategoryAnalyticsDto> TopCategories { get; set; } = new();
}
