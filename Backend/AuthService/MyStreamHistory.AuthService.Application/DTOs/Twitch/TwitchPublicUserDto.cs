using System.Text.Json.Serialization;

namespace MyStreamHistory.AuthService.Application.DTOs.Twitch;

public sealed class TwitchPublicUserDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("login")]
    public string? Login { get; init; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("profile_image_url")]
    public string? ProfileImageUrl { get; init; }
}
