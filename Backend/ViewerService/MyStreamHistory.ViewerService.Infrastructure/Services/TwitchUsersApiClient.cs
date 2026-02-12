using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyStreamHistory.ViewerService.Application.DTOs;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Infrastructure.Options;

namespace MyStreamHistory.ViewerService.Infrastructure.Services;

public class TwitchUsersApiClient : ITwitchUsersApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TwitchApiOptions _options;
    private readonly ILogger<TwitchUsersApiClient> _logger;

    public TwitchUsersApiClient(HttpClient httpClient, IOptions<TwitchApiOptions> options, ILogger<TwitchUsersApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<TwitchUserDto>> GetUsersByIdsAsync(List<string> userIds, string accessToken, CancellationToken cancellationToken = default)
    {
        if (!userIds.Any())
        {
            return new List<TwitchUserDto>();
        }

        // Twitch API allows max 100 users per request
        if (userIds.Count > 100)
        {
            _logger.LogWarning("Requested {Count} users, but Twitch API allows max 100. Taking first 100.", userIds.Count);
            userIds = userIds.Take(100).ToList();
        }

        var url = $"{_options.BaseUrl}/users?";
        url += string.Join("&", userIds.Select(id => $"id={id}"));

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Client-Id", _options.ClientId);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to get users: {StatusCode} - {Error}", response.StatusCode, error);
            throw new HttpRequestException($"Failed to get users: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<TwitchUsersResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        if (result?.Data == null)
        {
            return new List<TwitchUserDto>();
        }

        return result.Data.Select(u => new TwitchUserDto
        {
            Id = u.Id,
            Login = u.Login,
            DisplayName = u.DisplayName,
            ProfileImageUrl = u.ProfileImageUrl
        }).ToList();
    }

    private class TwitchUsersResponse
    {
        public List<UserData>? Data { get; set; }
    }

    private class UserData
    {
        public string Id { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ProfileImageUrl { get; set; } = string.Empty;
    }
}

