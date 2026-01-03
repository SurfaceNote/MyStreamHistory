using MyStreamHistory.AuthService.Application.DTOs.Twitch;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Domain.Entities;
using MyStreamHistory.Shared.Application.UnitOfWork;
using MyStreamHistory.Shared.Base.Error;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.AuthService.Application.Services;

public class AuthUserService(IAuthUserRepository authUserRepository, IUnitOfWork unitOfWork) : IAuthUserService
{
    public async Task<AuthUser> CreateAuthUserAsync(TwitchUserDto twitchUser, TokenResponseDto tokenResponse)
    {

        try
        {
            var user = await authUserRepository.AddAsync(
                new AuthUser
                {
                    DisplayName = twitchUser.DisplayName,
                    TwitchId = twitchUser.Id,
                    Email = twitchUser.Email,
                    IsTwitchTokenFresh = true,
                    LastActivityAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow,
                    Login = twitchUser.Login,
                    SiteCreatedAt = DateTime.UtcNow,
                    TwitchAccessToken = tokenResponse.AccessToken,
                    TwitchRefreshToken = tokenResponse.RefreshToken,
                    TwitchCreatedAt = twitchUser.CreatedAt
                }
            );
            
            return user;
        }
        catch (Exception ex)
        {
            throw new AppException(ErrorCodes.InternalError);
        }

        
    }
}