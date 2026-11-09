using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyStreamHistory.AdminService.Api;
using MyStreamHistory.Shared.Api.Authorization;
using Xunit;

namespace MyStreamHistory.AdminService.Tests;

public class AdminSecurityTests
{
    private const string Password = "integration-test-password-only";
    private static readonly string Hash = AdminPassword.Hash(Password);
    private static readonly string Key = new('k', 64);

    [Fact]
    public void PasswordHashIsSaltedAndRejectsIncorrectPasswords()
    {
        var second = AdminPassword.Hash(Password);
        Assert.NotEqual(Hash, second);
        Assert.True(AdminPassword.Verify(Password, Hash));
        Assert.False(AdminPassword.Verify("wrong password", Hash));
        Assert.False(AdminPassword.Verify(Password, "invalid"));
        Assert.False(AdminPassword.ValidHash(Hash.Replace("600000", "1")));
    }

    [Theory]
    [InlineData("GET", "content/admin/articles", true)]
    [InlineData("GET", "admin/streams", true)]
    [InlineData("POST", "admin/streams", false)]
    [InlineData("DELETE", "admin/streams", false)]
    [InlineData("GET", "admin/streams/5f0b1c2d-3e4f-4a5b-8c6d-7e8f9a0b1c2d/recap", true)]
    [InlineData("POST", "admin/streams/5f0b1c2d-3e4f-4a5b-8c6d-7e8f9a0b1c2d/recap", false)]
    [InlineData("GET", "admin/streams/not-a-guid/recap", false)]
    [InlineData("GET", "admin/streams/5f0b1c2d-3e4f-4a5b-8c6d-7e8f9a0b1c2d/recap/../x", false)]
    [InlineData("GET", "admin/product/overview", true)]
    [InlineData("POST", "admin/product/overview", false)]
    [InlineData("GET", "admin/bots", true)]
    [InlineData("POST", "admin/bots", true)]
    [InlineData("PUT", "admin/bots", false)]
    [InlineData("DELETE", "admin/bots", false)]
    [InlineData("DELETE", "admin/bots/nightbot", true)]
    [InlineData("DELETE", "admin/bots/Night-Bot", false)]
    [InlineData("DELETE", "admin/bots/../streams", false)]
    [InlineData("GET", "admin/bots/nightbot", false)]
    [InlineData("GET", "content/media/00000000-0000-0000-0000-000000000001", true)]
    [InlineData("DELETE", "diagnostics/eventsub-subscriptions", true)]
    [InlineData("POST", "analytics/community/settings", false)]
    [InlineData("PUT", "analytics/community/settings", false)]
    [InlineData("GET", "auth/refresh-token", false)]
    [InlineData("GET", "https://attacker.invalid/", false)]
    [InlineData("GET", "content/admin/articles/../../auth", false)]
    [InlineData("POST", "content/admin/articles/00000000-0000-0000-0000-000000000001/preview", false)]
    public void ProxyOnlyExposesExplicitMethodsAndResources(string method, string path, bool expected) =>
        Assert.Equal(expected, AdminProxy.Match(method, path) != null);

    [Fact]
    public async Task SessionRequiresLoginAndCsrfAndPreservesIdentityBoundTokens()
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient(); client.BaseAddress = new("https://localhost");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", new { username = "operator", password = Password })).StatusCode);
        var csrf = await Csrf(client);
        client.DefaultRequestHeaders.Add("Cookie", csrf.Cookie);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { username = "operator", password = "incorrect" })).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "operator", password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var sessionHeader = login.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("__Host-msh_admin="));
        Assert.Contains("httponly", sessionHeader.ToLowerInvariant()); Assert.Contains("secure", sessionHeader.ToLowerInvariant());
        Assert.DoesNotContain("domain=", sessionHeader.ToLowerInvariant());
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", csrf.Cookie + "; " + sessionHeader.Split(';')[0]);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/session")).StatusCode);
        // Anonymous antiforgery token cannot authorize authenticated mutations.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
        var signedInCsrf = await Csrf(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", signedInCsrf.Token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
    }

    [Fact]
    public async Task LoginIsRateLimitedAndMissingMetricsAreExplicit()
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient(); client.BaseAddress = new("https://localhost");
        var csrf = await Csrf(client);
        client.DefaultRequestHeaders.Add("Cookie", csrf.Cookie); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.Token);
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { username = "unknown", password = "bad" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", new { username = "operator", password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/monitoring")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/internal/metrics")).StatusCode);
    }

    [Fact]
    public async Task CookieFromPreviousCredentialConfigurationIsRejected()
    {
        await using var app = await CreateApp();
        var cookies = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>().Get(AdminSession.Scheme);
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([
            new(ClaimTypes.Name, "operator"), new("credential_stamp", "previous-credentials")], AdminSession.Scheme)), AdminSession.Scheme);
        using var client = app.GetTestClient(); client.BaseAddress = new("https://localhost");
        client.DefaultRequestHeaders.Add("Cookie", "__Host-msh_admin=" + cookies.TicketDataFormat.Protect(ticket));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
    }

    [Fact]
    public async Task ForwardedHttpsAndClientAddressAreOnlyAcceptedFromTheConfiguredProxyNetwork()
    {
        await using var app = await CreateApp();
        foreach (var (proxy, trusted) in new[] { ("10.42.0.10", true), ("192.0.2.10", false) })
        {
            var context = await app.GetTestServer().SendAsync(context =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(proxy);
                context.Request.Scheme = "http"; context.Request.Method = "GET";
                context.Request.Host = new("localhost"); context.Request.Path = "/health/ready";
                context.Request.Headers["X-Forwarded-Proto"] = "https";
                context.Request.Headers["X-Forwarded-For"] = "203.0.113.42";
            });
            Assert.Equal(trusted, context.Request.IsHttps);
            Assert.Equal(trusted ? "203.0.113.42" : proxy, context.Connection.RemoteIpAddress!.ToString());
        }
    }

    [Fact]
    public async Task ProxyPreservesPayloadAndStatusButReplacesClientCredentialsAndDropsUpstreamCookies()
    {
        var upstream = new FakeUpstream();
        await using var app = await CreateApp(upstream);
        using var client = app.GetTestClient(); client.BaseAddress = new("https://localhost");
        var csrf = await Csrf(client);
        client.DefaultRequestHeaders.Add("Cookie", csrf.Cookie); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.Token);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "operator", password = Password });
        login.EnsureSuccessStatusCode();
        var cookie = login.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("__Host-msh_admin=")).Split(';')[0];
        client.DefaultRequestHeaders.Remove("Cookie"); client.DefaultRequestHeaders.Add("Cookie", csrf.Cookie + "; " + cookie);
        var authenticated = await Csrf(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", authenticated.Token);
        client.DefaultRequestHeaders.Authorization = new("Bearer", "old-twitch-token");
        var response = await client.PostAsJsonAsync("/api/content/admin/articles", new { title = "Test draft", revision = 2 });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal("/content/admin/articles", upstream.Path);
        Assert.Contains("Test draft", upstream.Body);
        Assert.DoesNotContain("old-twitch-token", upstream.Token);
        Assert.False(upstream.ForwardedCookie);
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(upstream.Token);
        Assert.Contains(jwt.Claims, c => c.Type == "sub" && c.Value == "operator");
        Assert.Contains(jwt.Claims, c => c.Type == AdminServiceAuthentication.PermissionClaim && c.Value == "content");
        Assert.DoesNotContain(jwt.Claims, c => c.Type == "TwitchId");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/auth/refresh-token")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/analytics/community/settings", new { timeZoneId = "UTC" })).StatusCode);
    }

    [Fact]
    public async Task InternalAuthenticationSeparatesPermissionsAndRejectsOldTwitchAdminTokens()
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["AdminServiceAuth:Key"] = Key });
        builder.Services.AddAdminServiceAuthentication(builder.Configuration);
        builder.Services.AddAuthorization(options => options.AddPolicy("content", p => p.AddAuthenticationSchemes(AdminServiceAuthentication.Scheme)
            .RequireAuthenticatedUser().RequireClaim(AdminServiceAuthentication.PermissionClaim, AdminServiceAuthentication.Content)));
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
        app.MapGet("/content", () => "ok").RequireAuthorization("content"); await app.StartAsync();
        using var client = app.GetTestClient();
        var credentials = AdminServiceCredentials.Read(builder.Configuration);
        foreach (var token in new[] { credentials.Issue("operator", "diagnostics"),
            (credentials with { Audience = "wrong" }).Issue("operator", "content"),
            (credentials with { Key = new string('x', 64) }).Issue("operator", "content") })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            Assert.Contains((await client.GetAsync("/content")).StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized });
        }
        // A valid old-style admin role grants no administrative service permissions.
        client.DefaultRequestHeaders.Authorization = new("Bearer", credentials.Issue("operator", "admin"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/content")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", credentials.Issue("operator", "content"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/content")).StatusCode);
    }

    private static async Task<(string Cookie, string Token)> Csrf(HttpClient client)
    {
        var response = await client.GetAsync("/api/auth/csrf"); response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.First().Split(';')[0] : "", json.GetProperty("token").GetString()!);
    }

    private static async Task<WebApplication> CreateApp(IHttpClientFactory? clients = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["AdminAuth:Username"] = "operator", ["AdminAuth:PasswordHash"] = Hash,
            ["AdminServiceAuth:Key"] = Key, ["Services:Gateway"] = "http://localhost:5010", ["Services:Content"] = "http://localhost:5011",
            ["ReverseProxy:KnownNetworks:0"] = "10.42.0.0/24"
        });
        if (clients != null) builder.Services.AddSingleton(clients);
        var app = AdminApplication.Build(builder); await app.StartAsync(); return app;
    }

    private sealed class FakeUpstream : HttpMessageHandler, IHttpClientFactory
    {
        public string? Path; public string Body = ""; public string Token = ""; public bool ForwardedCookie;
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { BaseAddress = new("http://upstream.invalid/") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Path = request.RequestUri!.AbsolutePath; Body = await request.Content!.ReadAsStringAsync(ct);
            Token = request.Headers.Authorization!.Parameter!; ForwardedCookie = request.Headers.Contains("Cookie");
            var response = new HttpResponseMessage(HttpStatusCode.Conflict) { Content = JsonContent.Create(new { message = "Revision conflict." }) };
            response.Headers.TryAddWithoutValidation("Set-Cookie", "upstream-secret=must-not-reach-browser");
            return response;
        }
    }
}
