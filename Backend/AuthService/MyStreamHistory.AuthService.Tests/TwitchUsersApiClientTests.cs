using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Infrastructure.Options;
using MyStreamHistory.AuthService.Infrastructure.Services;
using Xunit;

namespace MyStreamHistory.AuthService.Tests;

public class TwitchUsersApiClientTests
{
    [Fact]
    public async Task ReadsStringIdsAndPublicProfile_UsingAppTokenAndIds()
    {
        var tokens = new Tokens();
        using var http = new HttpClient(new Handler(request =>
        {
            Assert.Equal("https://twitch.test/users?id=123&id=456", request.RequestUri!.ToString());
            Assert.Equal("Bearer cached", request.Headers.Authorization!.ToString());
            Assert.Equal("client", Assert.Single(request.Headers.GetValues("Client-Id")));
            return Json("""{"data":[{"id":"123","login":"new_login","display_name":"New Name","profile_image_url":"https://img.test/new.png"}]}""");
        }));

        var profiles = await Create(http, tokens).GetUsersAsync([123, 456]);

        var profile = Assert.Single(profiles);
        Assert.Equal("123", profile.Id);
        Assert.Equal("new_login", profile.Login);
        Assert.Equal("New Name", profile.DisplayName);
        Assert.Equal("https://img.test/new.png", profile.ProfileImageUrl);
        Assert.Equal([false], tokens.Refreshes);
    }

    [Fact]
    public async Task Unauthorized_RefreshesTokenOnce_AndDoesNotLoop()
    {
        var tokens = new Tokens();
        var calls = 0;
        using var http = new HttpClient(new Handler(request =>
        {
            calls++;
            Assert.Equal(calls == 1 ? "cached" : "fresh", request.Headers.Authorization!.Parameter);
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => Create(http, tokens).GetUsersAsync([1]));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Equal(2, calls);
        Assert.Equal([false, true], tokens.Refreshes);
    }

    [Fact]
    public async Task Unauthorized_RetriesSuccessfullyWithFreshToken()
    {
        var tokens = new Tokens();
        var calls = 0;
        using var http = new HttpClient(new Handler(_ => ++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized) : Json("""{"data":[]}""")));

        Assert.Empty(await Create(http, tokens).GetUsersAsync([1]));
        Assert.Equal([false, true], tokens.Refreshes);
    }

    [Fact]
    public async Task RateLimit_WaitsForLaterOfResetAndRetryAfter_ThenRetries()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var calls = 0;
        using var http = new HttpClient(new Handler(_ =>
        {
            if (++calls > 1)
                return Json("""{"data":[]}""");
            var limited = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(10));
            limited.Headers.Add("Ratelimit-Reset", clock.GetUtcNow().AddSeconds(30).ToUnixTimeSeconds().ToString());
            return limited;
        }));

        var result = Create(http, new Tokens(), clock).GetUsersAsync([1]);
        Assert.False(result.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.Equal(1, calls);
        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Empty(await result.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RateLimitWait_CanBeCancelled()
    {
        using var http = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        using var cancellation = new CancellationTokenSource();
        var result = Create(http, new Tokens(), new FakeTimeProvider()).GetUsersAsync([1], cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => result);
    }

    [Fact]
    public async Task EmptyInputDoesNotCallTwitch_AndOversizedBatchIsRejected()
    {
        using var http = new HttpClient(new Handler(_ => throw new Exception("Unexpected request")));
        var tokens = new Tokens();
        var client = Create(http, tokens);
        Assert.Empty(await client.GetUsersAsync([]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetUsersAsync(Enumerable.Range(1, 101).ToArray()));
        Assert.Empty(tokens.Refreshes);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":null}")]
    public async Task MissingDataIsNotTreatedAsSuccessfulEmptyResponse(string json)
    {
        using var http = new HttpClient(new Handler(_ => Json(json)));
        await Assert.ThrowsAsync<InvalidDataException>(() => Create(http, new Tokens()).GetUsersAsync([1]));
    }

    private static TwitchUsersApiClient Create(HttpClient http, Tokens tokens, TimeProvider? clock = null)
        => new(http, tokens, Options.Create(new TwitchOptions { ClientId = "client", UsersEndpoint = "https://twitch.test/users" }), clock ?? TimeProvider.System);

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(send(request));
    }

    private sealed class Tokens : ITwitchAppTokenService
    {
        public List<bool> Refreshes { get; } = [];
        public Task<(string AccessToken, DateTime ExpiresAt)> GetAppAccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            Refreshes.Add(forceRefresh);
            return Task.FromResult((forceRefresh ? "fresh" : "cached", DateTime.UtcNow.AddHours(1)));
        }
    }
}
