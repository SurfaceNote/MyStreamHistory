using MyStreamHistory.TwitchTrackingService.Application.DTOs;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface IUserProfileService
{
    /// <summary>
    /// Gets user profile data from AuthService by TwitchId
    /// </summary>
    /// <param name="twitchUserId">Twitch User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current public profile or null if not found</returns>
    Task<UserProfileDto?> GetUserProfileAsync(int twitchUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all users whose streams should be reconciled. A null result means
    /// the authoritative AuthService read failed and reverse reconciliation must be skipped.
    /// </summary>
    Task<IReadOnlyList<TrackedUserProfileDto>?> GetTrackedUsersAsync(
        CancellationToken cancellationToken = default);
}

