using Microsoft.Extensions.Logging;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Application.Services;

public class ViewerTrackingService : IViewerTrackingService
{
    private readonly IChatMessageBufferService _bufferService;
    private readonly ITwitchEventSubClient _eventSubClient;
    private readonly IAuthTokenService _authTokenService;
    private readonly ILogger<ViewerTrackingService> _logger;
    private readonly Dictionary<string, string> _activeSubscriptions = new(); // TwitchUserId -> SubscriptionId
    private readonly Dictionary<string, string> _cachedTokens = new(); // TwitchUserId -> AccessToken

    public ViewerTrackingService(
        IChatMessageBufferService bufferService,
        ITwitchEventSubClient eventSubClient,
        IAuthTokenService authTokenService,
        ILogger<ViewerTrackingService> logger)
    {
        _bufferService = bufferService;
        _eventSubClient = eventSubClient;
        _authTokenService = authTokenService;
        _logger = logger;
    }

    public async Task HandleStreamOnlineAsync(string twitchUserId, Guid streamSessionId, Guid? currentCategoryId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling stream online for TwitchUserId: {TwitchUserId}, StreamSessionId: {StreamSessionId}", twitchUserId, streamSessionId);

        // Initialize buffer
        _bufferService.InitializeStream(twitchUserId, streamSessionId, currentCategoryId);

        // Get access token
        var tokenResult = await _authTokenService.GetTwitchAccessTokenAsync(twitchUserId, cancellationToken);
        if (tokenResult == null)
        {
            _logger.LogError("Failed to get access token for TwitchUserId: {TwitchUserId}", twitchUserId);
            return;
        }

        var (accessToken, expiresAt) = tokenResult.Value;
        _cachedTokens[twitchUserId] = accessToken;

        // Subscribe to EventSub chat messages
        try
        {
            var subscriptionId = await _eventSubClient.SubscribeToChatMessagesAsync(twitchUserId, accessToken, cancellationToken);
            _activeSubscriptions[twitchUserId] = subscriptionId;
            _logger.LogInformation("Subscribed to chat messages for TwitchUserId: {TwitchUserId}, SubscriptionId: {SubscriptionId}", twitchUserId, subscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to chat messages for TwitchUserId: {TwitchUserId}", twitchUserId);
        }
    }

    public async Task HandleStreamOfflineAsync(string twitchUserId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Handling stream offline for TwitchUserId: {TwitchUserId}", twitchUserId);

        // Unsubscribe from EventSub
        string? subscriptionId = null;
        string? accessToken = null;

        // Try to get subscription ID from cache first
        if (_activeSubscriptions.TryGetValue(twitchUserId, out var cachedSubscriptionId))
        {
            subscriptionId = cachedSubscriptionId;
            _cachedTokens.TryGetValue(twitchUserId, out accessToken);
            _logger.LogInformation("Found cached subscription for TwitchUserId: {TwitchUserId}, SubscriptionId: {SubscriptionId}", 
                twitchUserId, subscriptionId);
        }
        else
        {
            // If not in cache, query Twitch API to find the subscription
            _logger.LogWarning("Subscription ID not found in cache for TwitchUserId: {TwitchUserId}, querying Twitch API", twitchUserId);
            
            try
            {
                // Get all chat message subscriptions
                var subscriptions = await _eventSubClient.GetSubscriptionsAsync("channel.chat.message", cancellationToken);
                
                // Find subscription for this broadcaster
                var subscription = subscriptions.FirstOrDefault(s => 
                    s.Condition?.BroadcasterUserId == twitchUserId && 
                    s.Status == "enabled");

                if (subscription != null)
                {
                    subscriptionId = subscription.Id;
                    _logger.LogInformation("Found subscription via API for TwitchUserId: {TwitchUserId}, SubscriptionId: {SubscriptionId}", 
                        twitchUserId, subscriptionId);
                }
                else
                {
                    _logger.LogWarning("No active chat message subscription found for TwitchUserId: {TwitchUserId}", twitchUserId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query subscriptions for TwitchUserId: {TwitchUserId}", twitchUserId);
            }
        }

        // Unsubscribe if we have a subscription ID
        if (!string.IsNullOrEmpty(subscriptionId))
        {
            try
            {
                await _eventSubClient.UnsubscribeAsync(subscriptionId, accessToken ?? string.Empty, cancellationToken);
                _logger.LogInformation("Successfully unsubscribed from chat messages for TwitchUserId: {TwitchUserId}, SubscriptionId: {SubscriptionId}", 
                    twitchUserId, subscriptionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unsubscribe from chat messages for TwitchUserId: {TwitchUserId}, SubscriptionId: {SubscriptionId}", 
                    twitchUserId, subscriptionId);
            }
        }

        // Clean up
        _activeSubscriptions.Remove(twitchUserId);
        _cachedTokens.Remove(twitchUserId);
        _bufferService.RemoveStream(twitchUserId);
    }
}

