using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Application.UnitOfWork;
using MyStreamHistory.Shared.Base.Contracts.Users.Requests;
using MyStreamHistory.Shared.Base.Contracts.Users.Response;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Services;

public class SubscriptionSyncService : ISubscriptionSyncService
{
    private readonly ITwitchApiClient _twitchApiClient;
    private readonly ITransportBus _transportBus;
    private readonly IEventSubSubscriptionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubscriptionSyncService> _logger;

    public SubscriptionSyncService(
        ITwitchApiClient twitchApiClient,
        ITransportBus transportBus,
        IEventSubSubscriptionRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<SubscriptionSyncService> logger)
    {
        _twitchApiClient = twitchApiClient;
        _transportBus = transportBus;
        _repository = repository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task SyncSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting EventSub subscriptions synchronization");

        try
        {
            // Get all users from AuthService
            var response = await _transportBus.SendRequestAsync<GetAllUsersRequestContract, GetAllUsersResponseContract>(
                new GetAllUsersRequestContract(),
                cancellationToken);

            if (response.Success == null || response.Success.Users == null || !response.Success.Users.Any())
            {
                _logger.LogWarning("No users found in AuthService");
                return;
            }

            var allUsersResponse = response.Success.Users;
            _logger.LogInformation("Found {UserCount} users to track", allUsersResponse.Count);

            // Get existing subscriptions from Twitch
            var existingUserIds = await _twitchApiClient.GetExistingSubscriptionsAsync(cancellationToken);

            // Create subscriptions for users that don't have them
            foreach (var user in allUsersResponse)
            {
                if (!existingUserIds.Contains(user.TwitchId))
                {
                    _logger.LogInformation("Creating subscriptions for user {DisplayName} (TwitchId: {TwitchId})", 
                        user.DisplayName, user.TwitchId);

                    // Create stream.online subscription
                    var onlineSubId = await _twitchApiClient.CreateEventSubSubscriptionAsync(
                        user.TwitchId, "stream.online", cancellationToken);

                    if (!string.IsNullOrEmpty(onlineSubId))
                    {
                        await _repository.AddAsync(new EventSubSubscription
                        {
                            TwitchSubscriptionId = onlineSubId,
                            TwitchUserId = user.TwitchId,
                            EventType = "stream.online",
                            Status = "enabled",
                            CreatedAt = DateTime.UtcNow
                        }, cancellationToken);
                    }

                    // Create stream.offline subscription
                    var offlineSubId = await _twitchApiClient.CreateEventSubSubscriptionAsync(
                        user.TwitchId, "stream.offline", cancellationToken);

                    if (!string.IsNullOrEmpty(offlineSubId))
                    {
                        await _repository.AddAsync(new EventSubSubscription
                        {
                            TwitchSubscriptionId = offlineSubId,
                            TwitchUserId = user.TwitchId,
                            EventType = "stream.offline",
                            Status = "enabled",
                            CreatedAt = DateTime.UtcNow
                        }, cancellationToken);
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("EventSub subscriptions synchronization completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during EventSub subscriptions synchronization");
            throw;
        }
    }
}

