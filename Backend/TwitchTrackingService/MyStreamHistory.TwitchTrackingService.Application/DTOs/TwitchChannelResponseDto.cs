using System.Text.Json.Serialization;

namespace MyStreamHistory.TwitchTrackingService.Application.DTOs;

/// <summary>Response of Twitch Get Channel Information (<c>helix/channels</c>).</summary>
public class TwitchChannelResponseDto
{
    [JsonPropertyName("data")]
    public List<TwitchChannelDto> Data { get; set; } = new();
}

public class TwitchChannelDto
{
    [JsonPropertyName("broadcaster_id")]
    public string BroadcasterId { get; set; } = null!;

    [JsonPropertyName("broadcaster_language")]
    public string? BroadcasterLanguage { get; set; }
}
