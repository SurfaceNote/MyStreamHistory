using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MyStreamHistory.Shared.Api.Authorization;

/// <summary>Independent trust boundary for server-to-server administrative calls.</summary>
public static class AdminServiceAuthentication
{
    public const string Scheme = "AdminService";
    public const string PermissionClaim = "admin_permission";
    public const string Diagnostics = "diagnostics";
    public const string Content = "content";
    public const string CommunityRead = "community.read";

    public static void AddAdminServiceAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = AdminServiceCredentials.Read(configuration);
        services.AddSingleton(settings);
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, AdminServiceTokenHandler>(Scheme, _ => { });
    }

    public static bool HasPermission(ClaimsPrincipal user, string permission) =>
        user.Identities.Any(identity => identity.IsAuthenticated && identity.AuthenticationType == Scheme
            && identity.HasClaim(PermissionClaim, permission));
}

public sealed record AdminServiceCredentials(string Key, string Issuer, string Audience)
{
    public bool Enabled => Key.Length > 0;
    public static AdminServiceCredentials Read(IConfiguration configuration)
    {
        var key = configuration["AdminServiceAuth:Key"] ?? "";
        if (key.Length > 0 && (Encoding.UTF8.GetByteCount(key) < 64 || key == configuration["Jwt:Key"]))
            throw new InvalidOperationException("AdminServiceAuth:Key must be a separate secret of at least 64 UTF-8 bytes.");
        return new(key, configuration["AdminServiceAuth:Issuer"] ?? "mystreamhistory-admin",
            configuration["AdminServiceAuth:Audience"] ?? "mystreamhistory-internal-admin");
    }

    public string Issue(string actor, string permission)
    {
        if (!Enabled) throw new InvalidOperationException("AdminServiceAuth:Key is required.");
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(Issuer, Audience,
            [new("sub", actor), new("UserName", actor), new(AdminServiceAuthentication.PermissionClaim, permission)],
            notBefore: now.AddSeconds(-5), expires: now.AddMinutes(1),
            signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

internal sealed class AdminServiceTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    AdminServiceCredentials credentials) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!credentials.Enabled || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());
        try
        {
            var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(header[7..],
                new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true, ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                    RequireExpirationTime = true, RequireSignedTokens = true,
                    ValidIssuer = credentials.Issuer, ValidAudience = credentials.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(credentials.Key)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.FromSeconds(5),
                    AuthenticationType = AdminServiceAuthentication.Scheme, NameClaimType = "UserName"
                }, out _);
            return Task.FromResult(AuthenticateResult.Success(new(principal, Scheme.Name)));
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid administrative service token."));
        }
    }
}
