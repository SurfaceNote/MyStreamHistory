namespace MyStreamHistory.TwitchTrackingService.Application.DTOs;

public class PlaythroughGameOptionDto
{
    public Guid TwitchCategoryId { get; set; }
    public string TwitchCategoryTwitchId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BoxArtUrl { get; set; } = string.Empty;
    public List<PlaythroughStreamCategoryDto> StreamCategories { get; set; } = new();
}
