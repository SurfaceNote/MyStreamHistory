namespace MyStreamHistory.TwitchTrackingService.Application.DTOs;

public class TwitchCategoryDto
{
    public string TwitchId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string BoxArtUrl { get; set; } = null!;
    public string? IgdbId { get; set; }
}

