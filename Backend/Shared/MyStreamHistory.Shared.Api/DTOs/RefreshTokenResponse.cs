namespace MyStreamHistory.Shared.Api.DTOs;

public class RefreshTokenResponse
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
}