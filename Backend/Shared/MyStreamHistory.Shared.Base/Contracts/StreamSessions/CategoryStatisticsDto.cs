namespace MyStreamHistory.Shared.Base.Contracts.StreamSessions;

public class CategoryStatisticsDto
{
    public Guid TwitchCategoryId { get; set; }
    public string TwitchId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string BoxArtUrl { get; set; } = null!;
    public string? IgdbId { get; set; }
    public double TotalHours { get; set; }
}

