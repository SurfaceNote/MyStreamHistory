using MassTransit;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Application.UnitOfWork;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;

namespace MyStreamHistory.AuthService.Api.Consumers;

public class LogoutConsumer(
    IRefreshTokenRepository refreshTokenRepository,
    IJwtTokenService jwtTokenService,
    IUnitOfWork unitOfWork
) : IConsumer<LogoutRequestContract>
{
    public async Task Consume(ConsumeContext<LogoutRequestContract> context)
    {
        if (jwtTokenService.TryReadRefreshTokenId(context.Message.Token, out var tokenId))
        {
            var tokenHash = jwtTokenService.HashRefreshToken(context.Message.Token);
            var refreshToken = await refreshTokenRepository.GetByTokenIdAndHashAsync(
                tokenId,
                tokenHash,
                context.CancellationToken);

            if (refreshToken != null && refreshToken.RevokedAt == null)
            {
                refreshToken.RevokedAt = DateTime.UtcNow;
                await unitOfWork.SaveChangesAsync();
            }
        }

        await context.RespondAsync(EmptyResponse.Instance);
    }
}
