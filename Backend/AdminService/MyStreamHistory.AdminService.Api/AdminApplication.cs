using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using MyStreamHistory.AdminService.Api;
using MyStreamHistory.Shared.Api.Authorization;
using MyStreamHistory.Shared.Api.Extensions;


namespace MyStreamHistory.AdminService.Api;

public static class AdminApplication
{
    public static WebApplication Build(WebApplicationBuilder builder)
    {

        var account = AdminAccount.Read(builder.Configuration);
        builder.Services.AddSingleton(account);
        var credentials = AdminServiceCredentials.Read(builder.Configuration);
        if (!credentials.Enabled) throw new InvalidOperationException("AdminServiceAuth:Key is required.");
        builder.Services.AddSingleton(credentials);
        var protection = builder.Services.AddDataProtection().SetApplicationName("MyStreamHistory.AdminService");
        var keysPath = builder.Configuration["AdminAuth:DataProtectionKeysPath"];
        if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(keysPath))
            throw new InvalidOperationException("AdminAuth:DataProtectionKeysPath must point to persistent storage.");
        if (!string.IsNullOrWhiteSpace(keysPath)) protection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
                options.KnownProxies.Add(IPAddress.Parse(proxy));
            foreach (var cidr in builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [])
            {
                var network = System.Net.IPNetwork.Parse(cidr);
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(network.BaseAddress, network.PrefixLength));
            }
        });
        builder.Services.AddAuthentication(AdminSession.Scheme).AddCookie(AdminSession.Scheme, options =>
        {
            options.Cookie.Name = "__Host-msh_admin";
            options.Cookie.HttpOnly = true; options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict; options.Cookie.Path = "/";
            options.ExpireTimeSpan = TimeSpan.FromHours(8); options.SlidingExpiration = false;
            options.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; },
                OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; },
                OnValidatePrincipal = async context =>
                {
                    if (context.Principal?.FindFirst("credential_stamp")?.Value != account.Stamp)
                    { context.RejectPrincipal(); await context.HttpContext.SignOutAsync(AdminSession.Scheme); }
                }
            };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-msh_admin_csrf";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 20 * 1024 * 1024);
        foreach (var (name, key) in new[] { ("gateway", "Services:Gateway"), ("content", "Services:Content") })
        {
            var raw = builder.Configuration[key];
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0
                || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0)
                throw new InvalidOperationException($"{key} must be a configured HTTP(S) service origin.");
            builder.Services.AddHttpClient(name, client => { client.BaseAddress = uri; client.Timeout = TimeSpan.FromSeconds(120); })
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        }
        builder.Services.AddAdminMonitoring(builder.Configuration);
        var app = builder.Build();
        app.UseForwardedHeaders();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["X-Request-ID"] = context.TraceIdentifier;
            await next();
        });
        app.UseRouting();
        app.UseServiceMetrics("admin");
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api") && context.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
            {
                try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
                catch (AntiforgeryValidationException) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { message = "Invalid CSRF token. Reload the page." }); return; }
            }
            await next();
        });
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapGet("/health/live", () => Results.Ok()).AllowAnonymous();
        app.MapGet("/health/ready", () => Results.Ok()).AllowAnonymous();
        app.MapAdminSessions();
        app.MapAdminMonitoring();
        app.MapAdminProxy();
        app.MapFallbackToFile("index.html");
        return app;
    }
}
