using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Infrastructure.Options;
using EventSubSubscription = MyStreamHistory.ViewerService.Application.Interfaces.EventSubSubscription;

namespace MyStreamHistory.ViewerService.Infrastructure.Services;

public class TwitchEventSubClient : ITwitchEventSubClient
{
    private readonly HttpClient _httpClient;
    private readonly TwitchApiOptions _options;
    private readonly ITwitchAppTokenService _appTokenService;
    private readonly ILogger<TwitchEventSubClient> _logger;

    public TwitchEventSubClient(
        HttpClient httpClient, 
        IOptions<TwitchApiOptions> options,
        ITwitchAppTokenService appTokenService,
        ILogger<TwitchEventSubClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _appTokenService = appTokenService;
        _logger = logger;
    }

    public async Task<string> SubscribeToChatMessagesAsync(string broadcasterId, string accessToken, CancellationToken cancellationToken = default)
    {
        // Get app access token for webhook subscription
        // Note: For webhook transport, Twitch requires app access token
        // But the broadcaster must have authorized the app with user:read:chat and user:bot scopes
        var appAccessToken = await _appTokenService.GetAppAccessTokenAsync(false, cancellationToken);
        
        var requestBody = new
        {
            type = "channel.chat.message",
            version = "1",
            condition = new
            {
                broadcaster_user_id = broadcasterId,
                user_id = broadcasterId
            },
            transport = new
            {
                method = "webhook",
                callback = _options.EventSubCallbackUrl,
                secret = _options.EventSubSecret
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/eventsub/subscriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", appAccessToken);
        request.Headers.Add("Client-Id", _options.ClientId);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to subscribe to chat messages: {StatusCode} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"Failed to subscribe to chat messages: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<EventSubSubscriptionResponse>(content, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        return result?.Data?.FirstOrDefault()?.Id ?? throw new InvalidOperationException("No subscription ID returned");
    }

    public async Task<List<EventSubSubscription>> GetSubscriptionsAsync(string? type = null, CancellationToken cancellationToken = default)
    {
        // Get app access token for webhook subscription management
        var appAccessToken = await _appTokenService.GetAppAccessTokenAsync(false, cancellationToken);
        
        var url = $"{_options.BaseUrl}/eventsub/subscriptions";
        if (!string.IsNullOrEmpty(type))
        {
            url += $"?type={type}";
        }

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", appAccessToken);
        request.Headers.Add("Client-Id", _options.ClientId);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to get subscriptions: {StatusCode} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"Failed to get subscriptions: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<EventSubSubscriptionsResponse>(content, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        return result?.Data ?? new List<EventSubSubscription>();
    }

    public async Task UnsubscribeAsync(string subscriptionId, string accessToken, CancellationToken cancellationToken = default)
    {
        // Get app access token for webhook subscription management
        var appAccessToken = await _appTokenService.GetAppAccessTokenAsync(false, cancellationToken);
        
        var request = new HttpRequestMessage(HttpMethod.Delete, $"{_options.BaseUrl}/eventsub/subscriptions?id={subscriptionId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", appAccessToken);
        request.Headers.Add("Client-Id", _options.ClientId);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to unsubscribe: {StatusCode} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"Failed to unsubscribe: {response.StatusCode}");
        }
        
        _logger.LogInformation("Successfully unsubscribed from EventSub subscription: {SubscriptionId}", subscriptionId);
    }

    public async Task<int> CleanupAllChatSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting cleanup of all chat message subscriptions");
        
        try
        {
            // Get all chat message subscriptions
            var subscriptions = await GetSubscriptionsAsync("channel.chat.message", cancellationToken);
            
            _logger.LogInformation("Found {Count} chat message subscriptions to clean up", subscriptions.Count);
            
            var deletedCount = 0;
            foreach (var subscription in subscriptions)
            {
                try
                {
                    await UnsubscribeAsync(subscription.Id, string.Empty, cancellationToken);
                    deletedCount++;
                    _logger.LogInformation("Deleted subscription {SubscriptionId} for broadcaster {BroadcasterId}", 
                        subscription.Id, subscription.Condition?.BroadcasterUserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete subscription {SubscriptionId}", subscription.Id);
                }
            }
            
            _logger.LogInformation("Cleanup completed: {DeletedCount} out of {TotalCount} subscriptions deleted", 
                deletedCount, subscriptions.Count);
            
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup chat subscriptions");
            throw;
        }
    }

    private class EventSubSubscriptionResponse
    {
        public List<SubscriptionData>? Data { get; set; }
    }

    private class SubscriptionData
    {
        public string Id { get; set; } = string.Empty;
    }

    private class EventSubSubscriptionsResponse
    {
        public List<EventSubSubscription>? Data { get; set; }
    }
}

