using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;
using MyStreamHistory.Shared.Base.Contracts.Auth.Responses;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Infrastructure.Services;

public class TwitchAppTokenService : ITwitchAppTokenService
{
    private readonly ITransportBus _transportBus;
    private readonly ILogger<TwitchAppTokenService> _logger;
    private string? _cachedToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public TwitchAppTokenService(
        ITransportBus transportBus,
        ILogger<TwitchAppTokenService> logger)
    {
        _transportBus = transportBus;
        _logger = logger;
    }

    public async Task<string> GetAppAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Check if cached token is still valid (with 5 minute buffer)
            if (!forceRefresh && _cachedToken != null && DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-5))
            {
                _logger.LogDebug("Returning cached app access token, expires at {ExpiresAt}", _tokenExpiresAt);
                return _cachedToken;
            }

            _logger.LogInformation("Requesting app access token from AuthService via RabbitMQ");

            // Request app access token from AuthService via RabbitMQ
            var response = await _transportBus.SendRequestAsync<GetAppAccessTokenRequestContract, GetAppAccessTokenResponseContract>(
                new GetAppAccessTokenRequestContract { ForceRefresh = forceRefresh },
                cancellationToken);

            _logger.LogDebug("Response from AuthService: IsSuccess={IsSuccess}, Success={Success}", 
                response.IsSuccess, response.Success != null ? "not null" : "null");

            if (response.Success != null)
            {
                _logger.LogDebug("Response details: Success={Success}, AccessToken={HasToken}, ExpiresAt={ExpiresAt}", 
                    response.Success.Success, !string.IsNullOrEmpty(response.Success.AccessToken), response.Success.ExpiresAt);
            }

            if (!response.IsSuccess || response.Success == null || !response.Success.Success || string.IsNullOrEmpty(response.Success.AccessToken))
            {
                var errorMessage = response.Success?.ErrorMessage ?? "Unknown error";
                _logger.LogError("Failed to get app access token from AuthService: {Error}", errorMessage);
                throw new InvalidOperationException($"Failed to get app access token: {errorMessage}");
            }

            _cachedToken = response.Success.AccessToken;
            _tokenExpiresAt = response.Success.ExpiresAt;

            _logger.LogInformation("Successfully obtained app access token from AuthService, expires at {ExpiresAt}", _tokenExpiresAt);

            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }
}

