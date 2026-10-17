using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyStreamHistory.ViewerService.Infrastructure.Options;
using MyStreamHistory.ViewerService.Infrastructure.Services;
using Xunit;

namespace MyStreamHistory.Community.Tests;

public class ChattersPaginationTests
{
    [Fact]
    public async Task PartialPaginationThrowsInsteadOfReturningPartialRoster()
    {
        var client = Client(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[{\"user_id\":\"1\",\"user_login\":\"one\",\"user_name\":\"One\"}],\"pagination\":{\"cursor\":\"next\"}}") },
            new HttpResponseMessage(HttpStatusCode.Unauthorized));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetChattersAsync("10", "test-token"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"data\":null}")]
    [InlineData("{\"data\":[{\"user_name\":\"No ID\"}]}")]
    public async Task MalformedSuccessfulResponseDoesNotMeanEmptyChat(string json)
    {
        var client = Client(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetChattersAsync("10", "test-token"));
    }

    [Fact]
    public async Task ValidEmptyRosterIsSuccessful()
    {
        var client = Client(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"data\":[],\"pagination\":{}}") });
        Assert.Empty(await client.GetChattersAsync("10", "test-token"));
    }

    private static TwitchChatApiClient Client(params HttpResponseMessage[] responses) => new(new HttpClient(new Handler(responses)),
        Options.Create(new TwitchApiOptions { BaseUrl = "https://example.invalid", ClientId = "test" }), NullLogger<TwitchChatApiClient>.Instance);
    private sealed class Handler(HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int index;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(responses[index++]);
    }
}
