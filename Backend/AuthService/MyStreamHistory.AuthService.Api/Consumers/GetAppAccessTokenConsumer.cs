using MassTransit;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;
using MyStreamHistory.Shared.Base.Contracts.Auth.Responses;

namespace MyStreamHistory.AuthService.Api.Consumers;

public class GetAppAccessTokenConsumer : IConsumer<GetAppAccessTokenRequestContract>
{
    private readonly ITwitchAppTokenService _twitchAppTokenService;
    private readonly ILogger<GetAppAccessTokenConsumer> _logger;

    public GetAppAccessTokenConsumer(
        ITwitchAppTokenService twitchAppTokenService,
        ILogger<GetAppAccessTokenConsumer> logger)
    {
        _twitchAppTokenService = twitchAppTokenService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetAppAccessTokenRequestContract> context)
    {
        _logger.LogInformation("Received GetAppAccessToken request, ForceRefresh: {ForceRefresh}", context.Message.ForceRefresh);

        try
        {
            var (accessToken, expiresAt) = await _twitchAppTokenService.GetAppAccessTokenAsync(
                context.Message.ForceRefresh, 
                context.CancellationToken);

            await context.RespondAsync(new GetAppAccessTokenResponseContract
            {
                AccessToken = accessToken,
                ExpiresAt = expiresAt,
                Success = true
            });

            _logger.LogInformation("Successfully returned app access token, expires at {ExpiresAt}", expiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get app access token");
            
            await context.RespondAsync(new GetAppAccessTokenResponseContract
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }
}

