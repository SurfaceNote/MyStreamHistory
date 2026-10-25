using MyStreamHistory.AuthService.Application.DTOs.Twitch;

namespace MyStreamHistory.AuthService.Application.Interfaces;

public interface ITwitchUsersApiClient
{
    Task<IReadOnlyList<TwitchPublicUserDto>> GetUsersAsync(
        IReadOnlyCollection<int> twitchUserIds, CancellationToken cancellationToken = default);
}
