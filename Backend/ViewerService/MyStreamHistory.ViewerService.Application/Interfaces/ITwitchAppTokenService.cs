namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface ITwitchAppTokenService
{
    /// <summary>
    /// Gets a valid Twitch app access token via RabbitMQ from AuthService
    /// </summary>
    Task<string> GetAppAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}

