using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace MyStreamHistory.AdminService.Api;

public sealed record AdminAccount(string Username, string PasswordHash)
{
    // Changing either configured credential invalidates previously issued cookies on their next use.
    public string Stamp => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Username + "\n" + PasswordHash)));
    public static AdminAccount Read(IConfiguration configuration)
    {
        var account = new AdminAccount(configuration["AdminAuth:Username"] ?? "", configuration["AdminAuth:PasswordHash"] ?? "");
        if (string.IsNullOrWhiteSpace(account.Username) || account.Username.Length > 100 || !AdminPassword.ValidHash(account.PasswordHash))
            throw new InvalidOperationException("Configure AdminAuth:Username and a valid AdminAuth:PasswordHash. Run --hash-password to generate a hash.");
        return account;
    }
}

public static class AdminSession
{
    public const string Scheme = "AdminSession";
    public static void MapAdminSessions(this WebApplication app)
    {
        // Anonymous bootstrap also protects login against login CSRF. It returns no session information.
        app.MapGet("/api/auth/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken })).AllowAnonymous();
        app.MapGet("/api/auth/session", (HttpContext context) =>
            Results.Ok(new { username = context.User.Identity!.Name })).RequireAuthorization();
        app.MapPost("/api/auth/login", async (LoginRequest input, HttpContext context, AdminAccount account, ILogger<AdminAccount> log) =>
        {
            // Always verify the password, including for unknown usernames.
            var valid = AdminPassword.Verify(input.Password ?? "", account.PasswordHash);
            if (!valid || input.Username != account.Username)
            {
                log.LogWarning("Admin login rejected; RequestId {RequestId}", context.TraceIdentifier);
                return Results.Unauthorized();
            }
            var identity = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, account.Username),
                new(ClaimTypes.Name, account.Username), new("credential_stamp", account.Stamp)], Scheme);
            await context.SignInAsync(Scheme, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = false });
            log.LogInformation("Admin login accepted for {Actor}; RequestId {RequestId}", account.Username, context.TraceIdentifier);
            return Results.Ok(new { username = account.Username });
        }).AllowAnonymous().RequireRateLimiting("login");
        app.MapPost("/api/auth/logout", async (HttpContext context, ILogger<AdminAccount> log) =>
        {
            log.LogInformation("Admin logout for {Actor}; RequestId {RequestId}", context.User.Identity!.Name, context.TraceIdentifier);
            await context.SignOutAsync(Scheme);
            return Results.NoContent();
        }).RequireAuthorization();
    }
    public sealed record LoginRequest(string? Username, string? Password);
}
