namespace MyStreamHistory.Gateway.Application.DTOs;

public class CategoryStatisticsDto
{
    public Guid TwitchCategoryId { get; set; }
    public string TwitchId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string BoxArtUrl { get; set; } = null!;
    public string? IgdbId { get; set; }
    public double TotalHours { get; set; }
}

