using System.Text.Json.Serialization;

namespace MyStreamHistory.AuthService.Application.DTOs.Twitch;

public class TwitchUserDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    
    [JsonPropertyName("login")]
    public string Login { get; set; } = null!;
    
    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = null!;
    
    [JsonPropertyName("profile_image_url")]
    public string ProfileImageUrl { get; set; } = null!;
    
    [JsonPropertyName("broadcaster_type")]
    public string BroadcasterType { get; set; } = null!;
    
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}