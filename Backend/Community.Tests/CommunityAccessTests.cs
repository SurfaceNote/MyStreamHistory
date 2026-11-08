using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyStreamHistory.Gateway.Api.Controllers;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Api.Authorization;
using Xunit;

namespace MyStreamHistory.Community.Tests;

public class CommunityAccessTests
{
    [Fact]
    public async Task AllCommunityRoutesRejectAnonymousRequests()
    {
        await using var app = await CreateApp(new FakeBus());
        using var client = app.GetTestClient();
        foreach (var (method, path) in new[]
        {
            ("GET", ""), ("GET", "/members"), ("PUT", "/settings"), ("GET", "/exclusions"),
            ("PUT", "/exclusions/1"), ("DELETE", "/exclusions/1")
        })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), "/analytics/community" + path);
            if (method == "PUT") request.Content = new StringContent("{\"timeZoneId\":\"UTC\"}", System.Text.Encoding.UTF8, "application/json");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
        }
    }

    [Fact]
    public async Task QueryAndBodyCannotOverrideAuthenticatedOwner()
    {
        var bus = new FakeBus();
        await using var app = await CreateApp(bus);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "123");
        var result = await client.GetAsync("/analytics/community?twitchId=999&streamerTwitchUserId=999");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("123", bus.LastRequest!.StreamerTwitchUserId);
        Assert.True(result.Headers.CacheControl!.NoStore);
        using var content = new StringContent("{\"timeZoneId\":\"UTC\",\"streamerTwitchUserId\":\"999\"}", System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsync("/analytics/community/settings", content)).StatusCode);
        Assert.Equal("123", bus.LastRequest!.StreamerTwitchUserId);
    }

    [Fact]
    public async Task OrdinaryUserCannotReadAnotherChannelEvenWithAnAdminQueryFlag()
    {
        var bus = new FakeBus();
        await using var app = await CreateApp(bus);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "123");
        foreach (var suffix in new[] { "", "/members", "/exclusions" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/analytics/community" + suffix + "?channelId=999&role=admin")).StatusCode);
        Assert.Null(bus.LastRequest);
    }

    [Fact]
    public async Task AdminCanReadAnotherChannelAndReceivesReadOnlyCapabilities()
    {
        var bus = new FakeBus();
        await using var app = await CreateApp(bus);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Credentials.Issue("operator", AdminServiceAuthentication.CommunityRead));
        foreach (var suffix in new[] { "", "/members", "/exclusions" })
        {
            var response = await client.GetAsync("/admin/community" + suffix + "?channelId=999");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("999", bus.LastRequest!.StreamerTwitchUserId);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(body.RootElement.GetProperty("data").GetProperty("canViewOtherChannels").GetBoolean());
            Assert.True(body.RootElement.GetProperty("data").GetProperty("readOnly").GetBoolean());
            Assert.True(response.Headers.CacheControl!.NoStore);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/admin/community")).StatusCode);
    }

    [Fact]
    public async Task AdminCannotApplyChangesToAnotherChannel()
    {
        var bus = new FakeBus();
        await using var app = await CreateApp(bus);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "admin");
        foreach (var (method, path) in new[] { ("PUT", "/settings"), ("PUT", "/exclusions/1"), ("DELETE", "/exclusions/1") })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), "/analytics/community" + path + "?channelId=999");
            if (method == "PUT") request.Content = new StringContent("{\"timeZoneId\":\"UTC\"}", System.Text.Encoding.UTF8, "application/json");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(request)).StatusCode);
        }
        Assert.Null(bus.LastRequest);
    }

    [Fact]
    public async Task OldTwitchAdminCannotReadAnotherChannelOrUseTheNewAdminApi()
    {
        var bus = new FakeBus();
        await using var app = await CreateApp(bus);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "admin");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/analytics/community?channelId=999")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/admin/community?channelId=999")).StatusCode);
        Assert.Null(bus.LastRequest);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    public async Task InvalidChannelDoesNotReachTheBus(string id)
    {
        var bus = new FakeBus();
        await using var app = await CreateApp(bus);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/analytics/community?channelId=" + id)).StatusCode);
        Assert.Null(bus.LastRequest);
    }

    [Fact]
    public async Task AuthenticatedIdentityWithoutTwitchClaimIsForbidden()
    {
        await using var app = await CreateApp(new FakeBus());
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "missing");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/analytics/community")).StatusCode);
    }

    private static readonly AdminServiceCredentials Credentials = new(new string('k', 64), "mystreamhistory-admin", "mystreamhistory-internal-admin");

    private static async Task<WebApplication> CreateApp(FakeBus bus)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ITransportBus>(bus);
        builder.Services.AddControllers().AddApplicationPart(typeof(CommunityController).Assembly);
        builder.Services.AddAuthentication("Tests").AddScheme<AuthenticationSchemeOptions, OwnerHandler>("Tests", _ => { });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["AdminServiceAuth:Key"] = Credentials.Key });
        builder.Services.AddAdminServiceAuthentication(builder.Configuration);
        builder.Services.AddAuthorization(options => options.AddPolicy("AdminCommunityRead", policy => policy
            .AddAuthenticationSchemes(AdminServiceAuthentication.Scheme).RequireAuthenticatedUser()
            .RequireClaim(AdminServiceAuthentication.PermissionClaim, AdminServiceAuthentication.CommunityRead)));
        var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private sealed class OwnerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var token = Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(token)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = token == "Bearer missing" ? new List<Claim>() : [new Claim("TwitchId", "123")];
            if (token == "Bearer admin") claims.Add(new Claim(ClaimTypes.Role, "admin"));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Tests"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "Tests")));
        }
    }
}
