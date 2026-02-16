using MassTransit;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Domain.Entities;
using MyStreamHistory.Shared.Application.UnitOfWork;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;
using MyStreamHistory.Shared.Base.Contracts.Auth.Responses;
using MyStreamHistory.Shared.Base.Error;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.AuthService.Api.Consumers;

public class RefreshTokenConsumer(
    IRefreshTokenRepository refreshTokenRepository,
    IJwtTokenService jwtTokenService,
    IUnitOfWork unitOfWork
    ) : IConsumer<RefreshTokenRequestContract>
{
    public async Task Consume(ConsumeContext<RefreshTokenRequestContract> context)
    {
        var existingToken = await refreshTokenRepository.GetByUserIdAndTokenAsync(
            context.Message.UserId, 
            context.Message.Token);

        if (existingToken == null)
        {
            throw new AppException(ErrorCodes.InvalidToken);
        }

        if (existingToken.ExpiresAt < DateTime.UtcNow)
        {
            refreshTokenRepository.Remove(existingToken);
            await unitOfWork.SaveChangesAsync();
            throw new AppException(ErrorCodes.TokenExpired);
        }

        if (existingToken.User == null)
        {
            throw new AppException(ErrorCodes.UserNotFound);
        }

        // Remove old refresh token
        refreshTokenRepository.Remove(existingToken);

        // Create new refresh token
        var newRefreshToken = new RefreshToken
        {
            Token = jwtTokenService.GenerateRefreshToken(),
            UserId = existingToken.UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(365)
        };

        await refreshTokenRepository.AddAsync(newRefreshToken);
        await unitOfWork.SaveChangesAsync();

        // Generate new access token
        var newAccessToken = jwtTokenService.GenerateAccessToken(existingToken.User);

        await context.RespondAsync(new RefreshTokenResponseContract
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken.Token
        });
    }
}