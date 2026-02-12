namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface IUserProfileService
{
    /// <summary>
    /// Gets user profile data from AuthService by TwitchId
    /// </summary>
    /// <param name="twitchUserId">Twitch User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User profile data (DisplayName, Avatar) or null if not found</returns>
    Task<(string DisplayName, string Avatar)?> GetUserProfileAsync(int twitchUserId, CancellationToken cancellationToken = default);
}

