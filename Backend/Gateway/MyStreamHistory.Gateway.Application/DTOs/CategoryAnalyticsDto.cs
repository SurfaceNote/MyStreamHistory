namespace MyStreamHistory.Gateway.Application.DTOs;

public class CategoryAnalyticsDto
{
    public Guid TwitchCategoryId { get; set; }
    public string TwitchId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string BoxArtUrl { get; set; } = null!;
    public double TotalHours { get; set; }
    public int StreamsCount { get; set; }
    public double AverageViewers { get; set; }
}

