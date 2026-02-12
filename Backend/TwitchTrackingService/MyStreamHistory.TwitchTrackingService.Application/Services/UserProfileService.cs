using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Users.Requests;
using MyStreamHistory.Shared.Base.Contracts.Users.Response;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Application.Services;

public class UserProfileService : IUserProfileService
{
    private readonly ITransportBus _transportBus;
    private readonly ILogger<UserProfileService> _logger;

    public UserProfileService(ITransportBus transportBus, ILogger<UserProfileService> logger)
    {
        _transportBus = transportBus;
        _logger = logger;
    }

    public async Task<(string DisplayName, string Avatar)?> GetUserProfileAsync(int twitchUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Requesting user profile for TwitchUserId: {TwitchUserId}", twitchUserId);

            var response = await _transportBus.SendRequestAsync<GetUserByTwitchIdRequestContract, GetUserByTwitchIdResponseContract>(
                new GetUserByTwitchIdRequestContract { TwitchId = twitchUserId },
                cancellationToken);

            if (response?.Success == null)
            {
                _logger.LogWarning("Received null or failed response when requesting user profile for TwitchUserId: {TwitchUserId}", twitchUserId);
                return null;
            }

            if (response.Success.User == null)
            {
                _logger.LogWarning("User not found for TwitchUserId: {TwitchUserId}", twitchUserId);
                return null;
            }

            _logger.LogDebug("Successfully retrieved user profile for TwitchUserId: {TwitchUserId}", twitchUserId);
            return (response.Success.User.DisplayName, response.Success.User.Avatar);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user profile for TwitchUserId: {TwitchUserId}", twitchUserId);
            return null;
        }
    }
}

