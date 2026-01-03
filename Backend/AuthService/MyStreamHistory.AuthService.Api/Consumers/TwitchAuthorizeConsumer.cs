using MassTransit;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Domain.Entities;
using MyStreamHistory.Shared.Application.UnitOfWork;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;
using MyStreamHistory.Shared.Base.Contracts.Auth.Responses;
using MyStreamHistory.Shared.Base.Error;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.AuthService.Api.Consumers;

public class TwitchAuthorizeConsumer(
    ITwitchAuthService twitchAuthService, 
    IAuthUserRepository authUserRepository, 
    IRefreshTokenRepository refreshTokenRepository,
    IAuthUserService authUserService,
    IJwtTokenService jwtTokenService,
    IUnitOfWork unitOfWork
    ) 
    : IConsumer<TwitchAuthorizeRequestContract>
{
    public async Task Consume(ConsumeContext<TwitchAuthorizeRequestContract> context)
    {
        var twitchTokenResponse = await twitchAuthService.ExchangeCodeForTokenAsync(context.Message.Code);

        if (twitchTokenResponse == null || string.IsNullOrEmpty(twitchTokenResponse.AccessToken))
        {
            throw new AppException(ErrorCodes.InvalidCredentials);
        }

        var twitchUser = await twitchAuthService.GetUserInfoAsync(twitchTokenResponse.AccessToken);
        if (twitchUser == null)
        {
            throw new AppException(ErrorCodes.InvalidCredentials);
        }

        var existingUser = await authUserRepository.GetUserByTwitchIdAsync(twitchUser.Id);

        var currentTime = DateTime.UtcNow;
        AuthUser user;

        if (existingUser == null)
        {
            user = await authUserService.CreateAuthUserAsync(twitchUser, twitchTokenResponse);
            await authUserRepository.AddAsync(user);
        }
        else
        {
            user = existingUser;
            user.TwitchAccessToken = twitchTokenResponse.AccessToken;
            user.TwitchRefreshToken = twitchTokenResponse.RefreshToken;
            user.LastLoginAt = currentTime;
            
            await authUserRepository.UpdateAsync(user);
        }

        var refreshToken = new RefreshToken
        {
            Token = jwtTokenService.GenerateRefreshToken(),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(365)
        };
        
        await refreshTokenRepository.AddAsync(refreshToken);
        
        await unitOfWork.SaveChangesAsync();
        
        await context.RespondAsync(new TwitchAuthorizeResponseContract
        {
            AccessToken = jwtTokenService.GenerateAccessToken(user),
            RefreshToken = refreshToken.Token
        });
        
        


    }
}