using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Options;

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Services;

public class TwitchApiClient : ITwitchApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TwitchApiOptions _options;
    private readonly ILogger<TwitchApiClient> _logger;
    private string? _appAccessToken;

    public TwitchApiClient(HttpClient httpClient, IOptions<TwitchApiOptions> options, ILogger<TwitchApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> CreateEventSubSubscriptionAsync(int broadcasterUserId, string eventType, CancellationToken cancellationToken = default)
    {
        await EnsureAppAccessTokenAsync(cancellationToken);

        var payload = new
        {
            type = eventType,
            version = "1",
            condition = new
            {
                broadcaster_user_id = broadcasterUserId.ToString()
            },
            transport = new
            {
                method = "webhook",
                callback = _options.CallbackUrl,
                secret = _options.WebhookSecret
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.EventSubEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _appAccessToken);
        request.Headers.Add("Client-Id", _options.ClientId);
        request.Content = JsonContent.Create(payload);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to create EventSub subscription. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                return null;
            }

            var responseContent = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var subscriptionId = responseContent.GetProperty("data")[0].GetProperty("id").GetString();

            _logger.LogInformation("Created EventSub subscription {SubscriptionId} for broadcaster {BroadcasterId}, type {EventType}", 
                subscriptionId, broadcasterUserId, eventType);

            return subscriptionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating EventSub subscription for broadcaster {BroadcasterId}, type {EventType}", 
                broadcasterUserId, eventType);
            return null;
        }
    }

    public async Task<List<int>> GetExistingSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAppAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, _options.EventSubEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _appAccessToken);
        request.Headers.Add("Client-Id", _options.ClientId);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get existing EventSub subscriptions. Status: {StatusCode}", response.StatusCode);
                return new List<int>();
            }

            var responseContent = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var userIds = new HashSet<int>();

            if (responseContent.TryGetProperty("data", out var data))
            {
                foreach (var subscription in data.EnumerateArray())
                {
                    if (subscription.TryGetProperty("condition", out var condition) &&
                        condition.TryGetProperty("broadcaster_user_id", out var userIdElement))
                    {
                        if (int.TryParse(userIdElement.GetString(), out var userId))
                        {
                            userIds.Add(userId);
                        }
                    }
                }
            }

            _logger.LogInformation("Found {Count} existing EventSub subscriptions", userIds.Count);
            return userIds.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting existing EventSub subscriptions");
            return new List<int>();
        }
    }

    public async Task DeleteSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        await EnsureAppAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{_options.EventSubEndpoint}?id={subscriptionId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _appAccessToken);
        request.Headers.Add("Client-Id", _options.ClientId);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Deleted EventSub subscription {SubscriptionId}", subscriptionId);
            }
            else
            {
                _logger.LogError("Failed to delete EventSub subscription {SubscriptionId}. Status: {StatusCode}", 
                    subscriptionId, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting EventSub subscription {SubscriptionId}", subscriptionId);
        }
    }

    public async Task<List<TwitchStreamDto>> GetStreamsAsync(List<int> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds == null || userIds.Count == 0)
        {
            return new List<TwitchStreamDto>();
        }

        await EnsureAppAccessTokenAsync(cancellationToken);

        var allStreams = new List<TwitchStreamDto>();

        // Split userIds into batches of 100 (Twitch API limit)
        var batches = userIds
            .Select((id, index) => new { id, index })
            .GroupBy(x => x.index / 100)
            .Select(g => g.Select(x => x.id).ToList())
            .ToList();

        _logger.LogInformation("Fetching streams for {TotalUsers} users in {BatchCount} batches", userIds.Count, batches.Count);

        foreach (var batch in batches)
        {
            try
            {
                var userIdsParam = string.Join("&", batch.Select(id => $"user_id={id}"));
                var url = $"https://api.twitch.tv/helix/streams?{userIdsParam}";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _appAccessToken);
                request.Headers.Add("Client-Id", _options.ClientId);

                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to get streams. Status: {StatusCode}, Error: {Error}", 
                        response.StatusCode, errorContent);
                    continue;
                }

                var streamsResponse = await response.Content.ReadFromJsonAsync<TwitchStreamsResponse>(cancellationToken: cancellationToken);
                
                if (streamsResponse?.Data != null)
                {
                    allStreams.AddRange(streamsResponse.Data);
                    _logger.LogInformation("Fetched {StreamCount} active streams from batch of {BatchSize} users", 
                        streamsResponse.Data.Count, batch.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching streams for batch");
            }
        }

        _logger.LogInformation("Total active streams fetched: {TotalStreams}", allStreams.Count);
        return allStreams;
    }

    private async Task EnsureAppAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_appAccessToken))
        {
            return;
        }

        var requestContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", _options.ClientId),
            new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

        using var response = await _httpClient.PostAsync(_options.TokenEndpoint, requestContent, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Failed to obtain app access token");
        }

        var tokenResponse = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        _appAccessToken = tokenResponse.GetProperty("access_token").GetString();

        _logger.LogInformation("Obtained Twitch app access token");
    }
}

