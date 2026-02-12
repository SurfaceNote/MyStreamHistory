using MyStreamHistory.ViewerService.Application.DTOs;

namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface ITwitchUsersApiClient
{
    /// <summary>
    /// Gets user information from Twitch API by user IDs
    /// </summary>
    /// <param name="userIds">List of Twitch user IDs (max 100)</param>
    /// <param name="accessToken">Twitch access token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user data</returns>
    Task<List<TwitchUserDto>> GetUsersByIdsAsync(List<string> userIds, string accessToken, CancellationToken cancellationToken = default);
}

