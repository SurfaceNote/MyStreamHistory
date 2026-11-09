using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using MyStreamHistory.Shared.Api.Authorization;

namespace MyStreamHistory.AdminService.Api;

public static partial class AdminProxy
{
    // Explicit route/method allowlist: no arbitrary upstream URLs or authentication endpoints.
    public static (string Client, string Permission)? Match(string method, string path)
    {
        if ((method == "GET" || method == "DELETE") && path is "diagnostics/eventsub-subscriptions" or "diagnostics/chat-subscriptions"
            || method == "POST" && path == "diagnostics/eventsub-subscriptions/subscribe-all")
            return ("gateway", AdminServiceAuthentication.Diagnostics);
        if (method == "GET" && path == "user/all") return ("gateway", AdminServiceAuthentication.CommunityRead);
        if (method == "GET" && (path == "admin/streams" || StreamRecapPath().IsMatch(path))) return ("gateway", AdminServiceAuthentication.StreamsRead);
        if (method == "GET" && path == "admin/product/overview") return ("gateway", AdminServiceAuthentication.ProductRead);
        if (path == "admin/bots" && method is "GET" or "POST" || method == "DELETE" && BotPath().IsMatch(path))
            return ("gateway", AdminServiceAuthentication.BotsManage);
        if (method == "GET" && path is "analytics/community" or "analytics/community/members" or "analytics/community/exclusions" or "analytics/community/overview")
            return ("gateway", AdminServiceAuthentication.CommunityRead);
        if (method == "GET" && MediaPath().IsMatch(path)) return ("content", AdminServiceAuthentication.Content);
        if (path == "content/admin/articles" && method is "GET" or "POST"
            || path == "content/admin/media" && method is "GET" or "POST"
            || ArticlePath().IsMatch(path) && method is "GET" or "PUT" or "DELETE"
            || ArticleActionPath().IsMatch(path) && method == "POST"
            || ArticlePreviewPath().IsMatch(path) && method == "GET"
            || AdminMediaPath().IsMatch(path) && method == "DELETE")
            return ("content", AdminServiceAuthentication.Content);
        return null;
    }

    public static void MapAdminProxy(this WebApplication app) => app.MapMethods("/api/{**path}", ["GET", "POST", "PUT", "DELETE"],
        async (string? path, HttpContext context, IHttpClientFactory clients, AdminServiceCredentials credentials, ILogger<AdminAccount> log) =>
        {
            var match = Match(context.Request.Method, path ?? "");
            if (match == null) { context.Response.StatusCode = 404; return; }
            var (clientName, permission) = match.Value;
            var upstreamPath = path!.StartsWith("analytics/community", StringComparison.Ordinal)
                ? "admin/community" + path["analytics/community".Length..] : path;
            using var request = new HttpRequestMessage(new(context.Request.Method), upstreamPath + context.Request.QueryString);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.Issue(context.User.Identity!.Name!, permission));
            request.Headers.Add("X-Correlation-ID", context.TraceIdentifier);
            if (context.Request.Headers.TryGetValue("X-Diagnostics-Confirmation", out var confirmation))
                request.Headers.TryAddWithoutValidation("X-Diagnostics-Confirmation", confirmation.ToArray());
            if (context.Request.Method is "POST" or "PUT")
            {
                request.Content = new StreamContent(context.Request.Body);
                if (context.Request.ContentType is { } contentType) request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                using var response = await clients.CreateClient(clientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
                context.Response.StatusCode = (int)response.StatusCode;
                if (response.Content.Headers.ContentType is { } type) context.Response.ContentType = type.ToString();
                await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
            catch (Exception exception) when (exception is HttpRequestException || exception is TaskCanceledException && !context.RequestAborted.IsCancellationRequested)
            {
                log.LogError("Admin upstream {Service} unavailable; RequestId {RequestId}", clientName, context.TraceIdentifier);
                if (!context.Response.HasStarted) { context.Response.StatusCode = 502; await context.Response.WriteAsJsonAsync(new { message = "Upstream service unavailable." }); }
                else context.Abort();
            }
            finally
            {
                log.LogInformation("AUDIT Admin {Actor} {Method} {Resource} -> {Status}; DurationMs {DurationMs}; RequestId {RequestId}",
                    context.User.Identity!.Name, context.Request.Method, path, context.Response.StatusCode,
                    System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds, context.TraceIdentifier);
            }
        }).RequireAuthorization();

    [GeneratedRegex(@"^admin/streams/[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}/recap$")]
    private static partial Regex StreamRecapPath();
    [GeneratedRegex(@"^admin/bots/[a-z0-9_]{1,25}$")]
    private static partial Regex BotPath();
    [GeneratedRegex(@"^content/media/[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$")]
    private static partial Regex MediaPath();
    [GeneratedRegex(@"^content/admin/articles/[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$")]
    private static partial Regex ArticlePath();
    [GeneratedRegex(@"^content/admin/articles/[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}/(?:publish|unpublish|restore|translations)$")]
    private static partial Regex ArticleActionPath();
    [GeneratedRegex(@"^content/admin/articles/[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}/preview$")]
    private static partial Regex ArticlePreviewPath();
    [GeneratedRegex(@"^content/admin/media/[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$")]
    private static partial Regex AdminMediaPath();
}
