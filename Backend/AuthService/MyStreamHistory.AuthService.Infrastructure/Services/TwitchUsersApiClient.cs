using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MyStreamHistory.AuthService.Application.DTOs.Twitch;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Infrastructure.Options;

namespace MyStreamHistory.AuthService.Infrastructure.Services;

public sealed class TwitchUsersApiClient(
    HttpClient httpClient,
    ITwitchAppTokenService tokenService,
    IOptions<TwitchOptions> options,
    TimeProvider timeProvider) : ITwitchUsersApiClient
{
    public async Task<IReadOnlyList<TwitchPublicUserDto>> GetUsersAsync(
        IReadOnlyCollection<int> twitchUserIds, CancellationToken cancellationToken = default)
    {
        if (twitchUserIds.Count == 0)
            return Array.Empty<TwitchPublicUserDto>();
        if (twitchUserIds.Count > 100 || twitchUserIds.Any(id => id <= 0))
            throw new ArgumentOutOfRangeException(nameof(twitchUserIds), "Provide 1 to 100 positive Twitch IDs.");

        var query = string.Join("&", twitchUserIds.Distinct().Select(id => $"id={id.ToString(CultureInfo.InvariantCulture)}"));
        var url = $"{options.Value.UsersEndpoint}{(options.Value.UsersEndpoint.Contains('?') ? "&" : "?")}{query}";
        var (token, _) = await tokenService.GetAppAccessTokenAsync(cancellationToken: cancellationToken);
        var refreshedToken = false;
        var retriedRateLimit = false;

        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("Client-Id", options.Value.ClientId);
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized && !refreshedToken)
            {
                (token, _) = await tokenService.GetAppAccessTokenAsync(true, cancellationToken);
                refreshedToken = true;
                continue;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests && !retriedRateLimit)
            {
                var delay = GetRateLimitDelay(response);
                await Task.Delay(delay, timeProvider, cancellationToken);
                retriedRateLimit = true;
                continue;
            }

            // Do not include Twitch response bodies or credentials in exceptions/logs.
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Twitch Get Users failed with HTTP {(int)response.StatusCode}.", null, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<UsersResponse>(cancellationToken: cancellationToken);
            return body?.Data ?? throw new InvalidDataException("Twitch Get Users returned no data array.");
        }
    }

    private TimeSpan GetRateLimitDelay(HttpResponseMessage response)
    {
        var now = timeProvider.GetUtcNow();
        var delay = response.Headers.RetryAfter?.Delta
            ?? (response.Headers.RetryAfter?.Date - now)
            ?? TimeSpan.FromSeconds(1);

        if (response.Headers.TryGetValues("Ratelimit-Reset", out var values)
            && long.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var reset)
            && reset >= DateTimeOffset.MinValue.ToUnixTimeSeconds()
            && reset <= DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            var resetDelay = DateTimeOffset.FromUnixTimeSeconds(reset) - now;
            if (resetDelay > delay)
                delay = resetDelay;
        }

        return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1);
    }

    private sealed class UsersResponse
    {
        [JsonPropertyName("data")]
        public List<TwitchPublicUserDto>? Data { get; init; }
    }
}
