using MassTransit;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;
using MyStreamHistory.Shared.Base.Contracts.Auth.Responses;

namespace MyStreamHistory.AuthService.Api.Consumers;

public class GetTwitchAccessTokenConsumer : IConsumer<GetTwitchAccessTokenRequestContract>
{
    private readonly IAuthUserRepository _authUserRepository;
    private readonly ILogger<GetTwitchAccessTokenConsumer> _logger;

    public GetTwitchAccessTokenConsumer(
        IAuthUserRepository authUserRepository,
        ILogger<GetTwitchAccessTokenConsumer> logger)
    {
        _authUserRepository = authUserRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetTwitchAccessTokenRequestContract> context)
    {
        var request = context.Message;
        
        _logger.LogInformation("Token request from {RequestingService} for TwitchUserId: {TwitchUserId}", 
            request.RequestingService, request.TwitchUserId);

        try
        {
            // Parse TwitchUserId to int
            if (!int.TryParse(request.TwitchUserId, out var twitchUserId))
            {
                _logger.LogWarning("Invalid TwitchUserId format: {TwitchUserId}", request.TwitchUserId);
                
                await context.RespondAsync(new GetTwitchAccessTokenResponseContract
                {
                    Success = false,
                    Error = $"Invalid TwitchUserId format: {request.TwitchUserId}"
                });
                return;
            }

            // Get user
            var user = await _authUserRepository.GetUserByTwitchIdAsync(twitchUserId);
            
            if (user == null)
            {
                _logger.LogWarning("User not found for TwitchUserId: {TwitchUserId}", twitchUserId);
                
                await context.RespondAsync(new GetTwitchAccessTokenResponseContract
                {
                    Success = false,
                    Error = $"User not found for TwitchUserId: {twitchUserId}"
                });
                return;
            }

            if (string.IsNullOrEmpty(user.TwitchAccessToken))
            {
                _logger.LogWarning("No Twitch access token for TwitchUserId: {TwitchUserId}", twitchUserId);
                
                await context.RespondAsync(new GetTwitchAccessTokenResponseContract
                {
                    Success = false,
                    Error = $"No Twitch access token for TwitchUserId: {twitchUserId}"
                });
                return;
            }

            // TODO: Add token expiration check and auto-refresh
            // For now, assume tokens don't expire (they do, but Twitch tokens are long-lived)
            
            await context.RespondAsync(new GetTwitchAccessTokenResponseContract
            {
                Success = true,
                AccessToken = user.TwitchAccessToken,
                ExpiresAt = DateTime.UtcNow.AddHours(4) // Twitch tokens typically last ~4 hours
            });

            _logger.LogDebug("Token provided to {RequestingService} for TwitchUserId: {TwitchUserId}", 
                request.RequestingService, twitchUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Twitch access token for TwitchUserId: {TwitchUserId}", 
                request.TwitchUserId);
            
            await context.RespondAsync(new GetTwitchAccessTokenResponseContract
            {
                Success = false,
                Error = $"Internal error: {ex.Message}"
            });
        }
    }
}

