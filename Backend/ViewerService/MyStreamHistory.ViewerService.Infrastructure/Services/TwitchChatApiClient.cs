using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Infrastructure.Options;

namespace MyStreamHistory.ViewerService.Infrastructure.Services;

public class TwitchChatApiClient : ITwitchChatApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TwitchApiOptions _options;
    private readonly ILogger<TwitchChatApiClient> _logger;

    public TwitchChatApiClient(HttpClient httpClient, IOptions<TwitchApiOptions> options, ILogger<TwitchChatApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<TwitchChatterDto>> GetChattersAsync(string broadcasterId, string accessToken, CancellationToken cancellationToken = default)
    {
        var chatters = new List<TwitchChatterDto>();
        string? cursor = null;

        do
        {
            var url = $"{_options.BaseUrl}/chat/chatters?broadcaster_id={broadcasterId}&moderator_id={broadcasterId}&first=1000";
            if (!string.IsNullOrEmpty(cursor))
            {
                url += $"&after={cursor}";
            }

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Add("Client-Id", _options.ClientId);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to get chatters: {StatusCode} - {Error}", response.StatusCode, error);
                throw new HttpRequestException($"Failed to get chatters: {response.StatusCode}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<TwitchChattersResponse>(content, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            if (result?.Data != null)
            {
                chatters.AddRange(result.Data.Select(c => new TwitchChatterDto
                {
                    UserId = c.UserId,
                    UserLogin = c.UserLogin,
                    UserName = c.UserName
                }));
            }

            cursor = result?.Pagination?.Cursor;

        } while (!string.IsNullOrEmpty(cursor));

        return chatters;
    }

    private class TwitchChattersResponse
    {
        public List<ChatterData>? Data { get; set; }
        public PaginationData? Pagination { get; set; }
    }

    private class ChatterData
    {
        public string UserId { get; set; } = string.Empty;
        public string UserLogin { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }

    private class PaginationData
    {
        public string? Cursor { get; set; }
    }
}

