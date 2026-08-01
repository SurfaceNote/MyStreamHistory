using Microsoft.Extensions.Logging;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Application.Services;

public class ViewerTrackingService : IViewerTrackingService
{
    private readonly IChatMessageBufferService _bufferService;
    private readonly ITwitchEventSubClient _eventSubClient;
    private readonly IAuthTokenService _authTokenService;
    private readonly IStreamLifecycleLock _lifecycleLock;
    private readonly ILogger<ViewerTrackingService> _logger;

    public ViewerTrackingService(
        IChatMessageBufferService bufferService,
        ITwitchEventSubClient eventSubClient,
        IAuthTokenService authTokenService,
        IStreamLifecycleLock lifecycleLock,
        ILogger<ViewerTrackingService> logger)
    {
        _bufferService = bufferService;
        _eventSubClient = eventSubClient;
        _authTokenService = authTokenService;
        _lifecycleLock = lifecycleLock;
        _logger = logger;
    }

    public async Task HandleStreamOnlineAsync(string twitchUserId, Guid streamSessionId, Guid? currentCategoryId, CancellationToken cancellationToken = default)
    {
        await using var lifecycleLock = await _lifecycleLock.AcquireAsync(twitchUserId, cancellationToken);

        _logger.LogInformation("Handling stream online for TwitchUserId: {TwitchUserId}, StreamSessionId: {StreamSessionId}", twitchUserId, streamSessionId);

        if (_bufferService.IsStreamActive(twitchUserId, streamSessionId))
        {
            if (currentCategoryId.HasValue)
            {
                _bufferService.UpdateStreamCategory(twitchUserId, streamSessionId, currentCategoryId.Value);
            }

            _logger.LogInformation("Stream buffer already active for TwitchUserId: {TwitchUserId}, refreshed category only", twitchUserId);
            return;
        }

        // Initialize buffer
        _bufferService.InitializeStream(twitchUserId, streamSessionId, currentCategoryId);

        // Get access token
        var tokenResult = await _authTokenService.GetTwitchAccessTokenAsync(twitchUserId, cancellationToken);
        if (tokenResult == null)
        {
            _logger.LogError("Failed to get access token for TwitchUserId: {TwitchUserId}", twitchUserId);
            return;
        }

        var (accessToken, _) = tokenResult.Value;
        // Subscribe to EventSub chat messages
        try
        {
            var existingSubscriptions = await _eventSubClient.GetSubscriptionsAsync("channel.chat.message", cancellationToken);
            var existingSubscription = existingSubscriptions.FirstOrDefault(s =>
                s.Condition?.BroadcasterUserId == twitchUserId &&
                s.Condition?.UserId == twitchUserId &&
                s.Status == "enabled");

            var subscriptionId = existingSubscription?.Id;
            if (string.IsNullOrEmpty(subscriptionId))
            {
                subscriptionId = await _eventSubClient.SubscribeToChatMessagesAsync(twitchUserId, accessToken, cancellationToken);
            }

            _logger.LogInformation("Subscribed to chat messages for TwitchUserId: {TwitchUserId}, SubscriptionId: {SubscriptionId}", twitchUserId, subscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to chat messages for TwitchUserId: {TwitchUserId}", twitchUserId);
        }
    }

    public async Task HandleStreamOfflineAsync(
        string twitchUserId,
        Guid streamSessionId,
        CancellationToken cancellationToken = default)
    {
        await using var lifecycleLock = await _lifecycleLock.AcquireAsync(twitchUserId, cancellationToken);

        _logger.LogInformation(
            "Handling stream offline for TwitchUserId: {TwitchUserId}, StreamSessionId: {StreamSessionId}",
            twitchUserId,
            streamSessionId);

        if (!_bufferService.IsStreamActive(twitchUserId, streamSessionId))
        {
            _logger.LogWarning(
                "Ignoring stale stream offline event for TwitchUserId {TwitchUserId}, StreamSessionId {StreamSessionId}",
                twitchUserId,
                streamSessionId);
            return;
        }

        string? subscriptionId = null;
        try
        {
            var subscriptions = await _eventSubClient.GetSubscriptionsAsync("channel.chat.message", cancellationToken);
            subscriptionId = subscriptions.FirstOrDefault(subscription =>
                subscription.Condition?.BroadcasterUserId == twitchUserId
                && subscription.Condition?.UserId == twitchUserId
                && subscription.Status == "enabled")?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query subscriptions for TwitchUserId: {TwitchUserId}", twitchUserId);
        }

        // Unsubscribe if we have a subscription ID
        if (!string.IsNullOrEmpty(subscriptionId))
        {
            try
            {
                await _eventSubClient.UnsubscribeAsync(subscriptionId, string.Empty, cancellationToken);
                _logger.LogInformation("Successfully unsubscribed from chat messages for TwitchUserId: {TwitchUserId}, SubscriptionId: {SubscriptionId}", 
                    twitchUserId, subscriptionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unsubscribe from chat messages for TwitchUserId: {TwitchUserId}, SubscriptionId: {SubscriptionId}", 
                    twitchUserId, subscriptionId);
            }
        }

        _bufferService.RemoveStream(twitchUserId, streamSessionId);
    }
}

