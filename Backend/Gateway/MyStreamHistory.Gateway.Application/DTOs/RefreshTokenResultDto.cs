namespace MyStreamHistory.Gateway.Application.DTOs;

public class RefreshTokenResultDto
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
}