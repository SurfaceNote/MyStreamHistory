using System.Text.Json.Serialization;

namespace MyStreamHistory.AuthService.Application.DTOs.Twitch;

public class TokenResponseDto
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;
    
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = null!;
    
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
    
    [JsonPropertyName("scope")]
    public string[] Scope { get; set; } = [];
    
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = null!;
}