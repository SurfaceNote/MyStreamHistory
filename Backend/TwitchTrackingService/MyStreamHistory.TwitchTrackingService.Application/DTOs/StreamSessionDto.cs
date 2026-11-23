namespace MyStreamHistory.TwitchTrackingService.Application.DTOs;

public class StreamSessionDto
{
    public Guid Id { get; set; }
    public string? StreamId { get; set; }
    public int TwitchUserId { get; set; }
    public string StreamerLogin { get; set; } = null!;
    public string StreamerDisplayName { get; set; } = null!;
    public string? StreamerAvatarUrl { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsLive { get; set; }
    public string? StreamTitle { get; set; }
    public string? GameName { get; set; }
    public int? ViewerCount { get; set; }
    /// <summary>Stream language from Twitch (ISO 639-1, e.g. "ru"; "other" when unspecified).</summary>
    public string? Language { get; set; }
    public List<TwitchCategoryDto> Categories { get; set; } = new();
}

