namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface IAuthTokenService
{
    Task<(string AccessToken, DateTime ExpiresAt)?> GetTwitchAccessTokenAsync(string twitchUserId, CancellationToken cancellationToken = default);
    Task<(string AccessToken, DateTime ExpiresAt)?> RefreshTwitchAccessTokenAsync(string twitchUserId, CancellationToken cancellationToken = default);
}

