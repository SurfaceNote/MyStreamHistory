namespace MyStreamHistory.AuthService.Application.Interfaces;

public interface ITwitchAppTokenService
{
    /// <summary>
    /// Gets a valid Twitch app access token. Returns cached token if still valid, otherwise requests a new one.
    /// </summary>
    /// <param name="forceRefresh">If true, forces refresh even if cached token is still valid</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of (AccessToken, ExpiresAt)</returns>
    Task<(string AccessToken, DateTime ExpiresAt)> GetAppAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}

