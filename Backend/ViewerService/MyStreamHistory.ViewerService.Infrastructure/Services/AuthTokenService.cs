using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;
using MyStreamHistory.Shared.Base.Contracts.Auth.Responses;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Infrastructure.Services;

public class AuthTokenService : IAuthTokenService
{
    private readonly ITransportBus _transportBus;
    private readonly ILogger<AuthTokenService> _logger;

    public AuthTokenService(
        ITransportBus transportBus,
        ILogger<AuthTokenService> logger)
    {
        _transportBus = transportBus;
        _logger = logger;
    }

    public async Task<(string AccessToken, DateTime ExpiresAt)?> GetTwitchAccessTokenAsync(string twitchUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _transportBus.SendRequestAsync<
                GetTwitchAccessTokenRequestContract,
                GetTwitchAccessTokenResponseContract>(
                new GetTwitchAccessTokenRequestContract
                {
                    TwitchUserId = twitchUserId,
                    RequestingService = "ViewerService"
                },
                cancellationToken);

            var result = response.Success;
            
            if (result == null || !result.Success || result.AccessToken == null || result.ExpiresAt == null)
            {
                _logger.LogError("Failed to get access token for TwitchUserId: {TwitchUserId}, Error: {Error}", 
                    twitchUserId, result?.Error);
                return null;
            }

            return (result.AccessToken, result.ExpiresAt.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while getting access token for TwitchUserId: {TwitchUserId}", twitchUserId);
            return null;
        }
    }

    public async Task<(string AccessToken, DateTime ExpiresAt)?> RefreshTwitchAccessTokenAsync(string twitchUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _transportBus.SendRequestAsync<
                RefreshTwitchAccessTokenRequestContract,
                RefreshTwitchAccessTokenResponseContract>(
                new RefreshTwitchAccessTokenRequestContract
                {
                    TwitchUserId = twitchUserId,
                    RequestingService = "ViewerService"
                },
                cancellationToken);

            var result = response.Success;
            
            if (result == null || !result.Success || result.AccessToken == null || result.ExpiresAt == null)
            {
                _logger.LogError("Failed to refresh access token for TwitchUserId: {TwitchUserId}, Error: {Error}", 
                    twitchUserId, result?.Error);
                return null;
            }

            return (result.AccessToken, result.ExpiresAt.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while refreshing access token for TwitchUserId: {TwitchUserId}", twitchUserId);
            return null;
        }
    }
}

