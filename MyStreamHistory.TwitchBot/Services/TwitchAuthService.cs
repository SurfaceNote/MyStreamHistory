using System.Text.Json;

namespace MyStreamHistory.TwitchBot.Services;

public class TwitchAuthService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<TwitchAuthService> logger)
{
    private string? _appAccessToken;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetAppAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_appAccessToken) || DateTimeOffset.UtcNow >= _expiresAt)
        {
            logger.LogInformation("GetAppAccessTokenAsync started");
            await RefreshAppAccessTokenAsync(cancellationToken);
        }
        
        return _appAccessToken!;
    }

    public async Task RefreshAppAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient();
        
        var clientId =  configuration["Twitch:ClientId"]!;
        var clientSecret =  configuration["Twitch:ClientSecret"]!;
        
        var t = configuration["Twitch:BotNickname"]!;
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["grant_type"] = "client_credentials"
        });
        
        logger.LogInformation("RefreshAppAccessTokenAsync started");
        var response = await client.PostAsync("https://id.twitch.tv/oauth2/token", content, cancellationToken);
        logger.LogInformation($"Response code: {response.StatusCode}");
        response.EnsureSuccessStatusCode();
        

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;
        
        _appAccessToken = root.GetProperty("access_token").GetString();
        var expiresIn = root.GetProperty("expires_in").GetInt32();
        _expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        logger.LogInformation("Twitch AccessToken is refreshed");
        if (!client.DefaultRequestHeaders.Contains("Client-ID"))
        {
            client.DefaultRequestHeaders.Add("Client-ID", clientId);
        }
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _appAccessToken);
    }
}