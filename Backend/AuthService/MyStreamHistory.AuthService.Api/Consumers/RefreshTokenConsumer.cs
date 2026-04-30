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
        var now = DateTime.UtcNow;
        if (!jwtTokenService.TryReadRefreshTokenId(context.Message.Token, out var tokenId))
        {
            throw new AppException(ErrorCodes.InvalidToken);
        }

        var tokenHash = jwtTokenService.HashRefreshToken(context.Message.Token);
        var existingToken = await refreshTokenRepository.GetByTokenIdAndHashAsync(
            tokenId,
            tokenHash,
            context.CancellationToken);

        var expiredTokens = await refreshTokenRepository.GetExpiredAsync(now, context.CancellationToken);
        var expiredTokensToRemove = expiredTokens
            .Where(rt => rt.TokenId != tokenId)
            .ToList();

        if (expiredTokensToRemove.Count > 0)
        {
            refreshTokenRepository.RemoveRange(expiredTokensToRemove);
        }

        if (existingToken == null)
        {
            throw new AppException(ErrorCodes.InvalidToken);
        }

        if (existingToken.RevokedAt != null || existingToken.ReplacedByTokenId != null)
        {
            await RevokeFamilyAsync(existingToken.TokenFamilyId, now, context.CancellationToken);
            await unitOfWork.SaveChangesAsync();
            throw new AppException(ErrorCodes.InvalidToken);
        }

        if (existingToken.ExpiresAt < now)
        {
            existingToken.RevokedAt = now;
            await unitOfWork.SaveChangesAsync();
            throw new AppException(ErrorCodes.TokenExpired);
        }

        if (existingToken.User == null)
        {
            throw new AppException(ErrorCodes.UserNotFound);
        }

        var newRefreshTokenId = Guid.NewGuid();
        var newRefreshTokenValue = jwtTokenService.GenerateRefreshToken(newRefreshTokenId);
        
        existingToken.RevokedAt = now;
        existingToken.ReplacedByTokenId = newRefreshTokenId;

        var newRefreshToken = new RefreshToken
        {
            TokenId = newRefreshTokenId,
            TokenFamilyId = existingToken.TokenFamilyId,
            TokenHash = jwtTokenService.HashRefreshToken(newRefreshTokenValue),
            UserId = existingToken.UserId,
            CreatedAt = now,
            ExpiresAt = now.AddDays(365),
            CreatedByIp = context.Message.CreatedByIp
        };

        await refreshTokenRepository.AddAsync(newRefreshToken);
        await unitOfWork.SaveChangesAsync();

        // Generate new access token
        var newAccessToken = jwtTokenService.GenerateAccessToken(existingToken.User);

        await context.RespondAsync(new RefreshTokenResponseContract
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshTokenValue
        });
    }

    private async Task RevokeFamilyAsync(Guid tokenFamilyId, DateTime revokedAt, CancellationToken cancellationToken)
    {
        var tokenFamily = await refreshTokenRepository.GetByFamilyIdAsync(tokenFamilyId, cancellationToken);

        foreach (var refreshToken in tokenFamily.Where(rt => rt.RevokedAt == null))
        {
            refreshToken.RevokedAt = revokedAt;
        }
    }
}
