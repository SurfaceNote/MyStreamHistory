namespace MyStreamHistory.Gateway.Application.DTOs;

public class PlaythroughStreamCategoryDto
{
    public Guid StreamCategoryId { get; set; }
    public Guid StreamSessionId { get; set; }
    public Guid TwitchCategoryId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string StreamTitle { get; set; } = string.Empty;
    public DateTime StreamStartedAt { get; set; }
    public DateTime? StreamEndedAt { get; set; }
    public DateTime CategoryStartedAt { get; set; }
    public DateTime? CategoryEndedAt { get; set; }
}
