using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http.Features;

namespace MyStreamHistory.Gateway.Api;

public static class ContentProxyEndpoint
{
    private const long MaxRequestBytes = 21L * 1024 * 1024;

    public static void MapContentProxy(this WebApplication app)
    {
        var methods = new[] { "GET", "POST", "PUT", "DELETE" };
        app.MapMethods("/content", methods, Forward);
        app.MapMethods("/content/{**path}", methods, Forward);
    }

    private static async Task Forward(HttpContext context, IHttpClientFactory clients,
        ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var incoming = context.Request;
        if (incoming.ContentLength > MaxRequestBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        var limit = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false }) limit.MaxRequestBodySize = MaxRequestBytes;

        var path = context.Request.RouteValues["path"]?.ToString();
        var target = "content" + (string.IsNullOrEmpty(path) ? "" : "/" + path) + incoming.QueryString;
        using var request = new HttpRequestMessage(new HttpMethod(incoming.Method), target);
        if (incoming.Headers.TryGetValue("Authorization", out var authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization.ToString());
        if (incoming.ContentLength > 0 || incoming.Headers.ContainsKey("Transfer-Encoding"))
        {
            request.Content = new StreamContent(incoming.Body);
            if (incoming.ContentType is not null)
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(incoming.ContentType);
            if (incoming.ContentLength is long length)
                request.Content.Headers.ContentLength = length;
        }

        try
        {
            using var response = await clients.CreateClient("content")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.Headers.XContentTypeOptions = "nosniff";
            if (response.Content.Headers.ContentType is not null)
                context.Response.ContentType = response.Content.Headers.ContentType.ToString();
            if (response.Headers.Location is not null)
                context.Response.Headers.Location = response.Headers.Location.ToString();
            if (response.Headers.CacheControl is not null)
                context.Response.Headers.CacheControl = response.Headers.CacheControl.ToString();
            await response.Content.CopyToAsync(context.Response.Body, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            loggers.CreateLogger("ContentProxy").LogError(exception, "Content Service request failed");
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
        }
    }
}
