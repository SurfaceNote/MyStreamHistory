using MyStreamHistory.AuthService.Application.DTOs.Twitch;

namespace MyStreamHistory.AuthService.Application.Interfaces;

public interface ITwitchAuthService
{
    Task<TokenResponseDto?> ExchangeCodeForTokenAsync(string code);
    Task<TwitchUserDto?> GetUserInfoAsync(string accessToken);
}