namespace MyStreamHistory.Gateway.Application.DTOs;

public class PlaythroughDto
{
    public Guid Id { get; set; }
    public Guid TwitchCategoryId { get; set; }
    public string TwitchCategoryTwitchId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string BoxArtUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool AutoAddNewStreams { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<PlaythroughStreamCategoryDto> StreamCategories { get; set; } = new();
}
