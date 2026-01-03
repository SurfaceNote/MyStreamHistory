using System.Text.Json.Serialization;

namespace MyStreamHistory.AuthService.Application.DTOs.Twitch;

public class TwitchUserResponseDto
{
    [JsonPropertyName("data")] 
    public List<TwitchUserDto> TwitchUsers { get; set; } = [];
}