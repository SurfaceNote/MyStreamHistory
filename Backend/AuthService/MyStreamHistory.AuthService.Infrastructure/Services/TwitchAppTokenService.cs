using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Infrastructure.Options;

namespace MyStreamHistory.AuthService.Infrastructure.Services;

public class TwitchAppTokenService : ITwitchAppTokenService
{
    private readonly HttpClient _httpClient;
    private readonly TwitchOptions _options;
    private readonly ILogger<TwitchAppTokenService> _logger;
    private string? _cachedToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public TwitchAppTokenService(HttpClient httpClient, IOptions<TwitchOptions> options, ILogger<TwitchAppTokenService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<(string AccessToken, DateTime ExpiresAt)> GetAppAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Check if cached token is still valid (with 5 minute buffer)
            if (!forceRefresh && _cachedToken != null && DateTime.UtcNow < _tokenExpiresAt.AddMinutes(-5))
            {
                _logger.LogDebug("Returning cached app access token, expires at {ExpiresAt}", _tokenExpiresAt);
                return (_cachedToken, _tokenExpiresAt);
            }

            _logger.LogInformation("Requesting new app access token from Twitch");

            // Request new app access token
            var request = new HttpRequestMessage(HttpMethod.Post, "https://id.twitch.tv/oauth2/token");
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _options.ClientId),
                new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to get app access token: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"Failed to get app access token: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<TwitchTokenResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.AccessToken == null)
            {
                _logger.LogError("No access token returned from Twitch");
                throw new InvalidOperationException("No access token returned");
            }

            _cachedToken = result.AccessToken;
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(result.ExpiresIn);

            _logger.LogInformation("Successfully obtained app access token, expires in {ExpiresIn} seconds ({ExpiresAt})", result.ExpiresIn, _tokenExpiresAt);

            return (_cachedToken, _tokenExpiresAt);
        }
        finally
        {
            _lock.Release();
        }
    }

    private class TwitchTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
        
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
        
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
    }
}

