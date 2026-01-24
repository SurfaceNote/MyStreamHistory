using System.Text.Json.Serialization;

namespace MyStreamHistory.TwitchTrackingService.Application.DTOs;

public class TwitchStreamDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = null!;

    [JsonPropertyName("user_login")]
    public string UserLogin { get; set; } = null!;

    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = null!;

    [JsonPropertyName("game_id")]
    public string GameId { get; set; } = null!;

    [JsonPropertyName("game_name")]
    public string GameName { get; set; } = null!;

    [JsonPropertyName("type")]
    public string Type { get; set; } = null!; // "live" or empty for offline

    [JsonPropertyName("title")]
    public string Title { get; set; } = null!;

    [JsonPropertyName("viewer_count")]
    public int ViewerCount { get; set; }

    [JsonPropertyName("started_at")]
    public DateTime StartedAt { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; } = null!;

    [JsonPropertyName("thumbnail_url")]
    public string ThumbnailUrl { get; set; } = null!;

    [JsonPropertyName("tag_ids")]
    public List<string> TagIds { get; set; } = new();

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("is_mature")]
    public bool IsMature { get; set; }
}

public class TwitchStreamsResponse
{
    [JsonPropertyName("data")]
    public List<TwitchStreamDto> Data { get; set; } = new();

    [JsonPropertyName("pagination")]
    public TwitchPagination? Pagination { get; set; }
}

public class TwitchPagination
{
    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

