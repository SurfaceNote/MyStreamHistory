using MyStreamHistory.AuthService.Application.DTOs.Twitch;
using MyStreamHistory.AuthService.Domain.Entities;

namespace MyStreamHistory.AuthService.Application.Interfaces;

public interface IAuthUserService
{
    Task<AuthUser> CreateAuthUserAsync(TwitchUserDto twitchUser, TokenResponseDto tokenResponse);
}