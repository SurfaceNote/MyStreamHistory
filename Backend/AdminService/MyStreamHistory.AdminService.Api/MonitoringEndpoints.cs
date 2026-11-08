using System.Globalization;
using System.Text.Json;

namespace MyStreamHistory.AdminService.Api;

public static class MonitoringEndpoints
{
    public static void AddAdminMonitoring(this IServiceCollection services, IConfiguration configuration)
    {
        var origin = configuration["Monitoring:PrometheusUrl"];
        if (!string.IsNullOrWhiteSpace(origin) && (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length > 0 || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0))
            throw new InvalidOperationException("Monitoring:PrometheusUrl must be a trusted HTTP(S) origin.");
        services.AddHttpClient("prometheus", client =>
        {
            if (!string.IsNullOrWhiteSpace(origin)) client.BaseAddress = new Uri(origin);
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
    }

    public static void MapAdminMonitoring(this WebApplication app)
    {
        app.MapGet("/api/monitoring", async (string? range, IConfiguration configuration, IHttpClientFactory clients, CancellationToken ct) =>
        {
            var window = range switch { null or "1h" => "1h", "24h" => "24h", "7d" => "7d", _ => null };
            if (window == null) return Results.BadRequest(new { message = "Invalid range." });
            var sentry = configuration["Monitoring:SentryUrl"];
            if (!Uri.TryCreate(sentry, UriKind.Absolute, out var link) || link.Scheme != "https" || link.UserInfo.Length > 0) sentry = null;
            if (string.IsNullOrWhiteSpace(configuration["Monitoring:PrometheusUrl"]))
                return Results.Ok(new { state = "unconfigured", sentryUrl = sentry, updatedAt = DateTimeOffset.UtcNow });
            var queries = new Dictionary<string, string>
            {
                ["ssrRps"] = $"sum(rate(msh_ssr_render_duration_seconds_count[{window}]))",
                ["ssrP50"] = $"histogram_quantile(0.5,sum by(le)(rate(msh_ssr_render_duration_seconds_bucket[{window}])))",
                ["ssrP95"] = $"histogram_quantile(0.95,sum by(le)(rate(msh_ssr_render_duration_seconds_bucket[{window}])))",
                ["ssrErrors"] = $"(sum(rate(msh_ssr_render_duration_seconds_count{{status_class=\"5xx\"}}[{window}])) or vector(0)) / sum(rate(msh_ssr_render_duration_seconds_count[{window}]))",
                ["ssrApiPerPage"] = $"sum(rate(msh_ssr_api_duration_seconds_count[{window}])) / sum(rate(msh_ssr_render_duration_seconds_count[{window}]))",
                ["ssrHtmlBytes"] = $"sum(rate(msh_ssr_html_bytes_total[{window}])) / sum(rate(msh_ssr_render_duration_seconds_count[{window}]))",
                ["ssrMemoryBytes"] = "sum(msh_ssr_process_resident_memory_bytes)",
                ["ssrCpuCores"] = "sum(rate(msh_ssr_process_cpu_seconds_total[5m]))",
                ["eventLoopDelay"] = "max(msh_ssr_event_loop_delay_seconds)",
                ["apiP95"] = $"histogram_quantile(0.95,sum by(le,service,route)(rate(msh_http_request_duration_seconds_bucket[{window}])))",
                ["pageP95"] = $"histogram_quantile(0.95,sum by(le,route,language)(rate(msh_ssr_render_duration_seconds_bucket[{window}])))",
                ["scrapes"] = "up{job=~\"msh-.*\"}"
            };
            var client = clients.CreateClient("prometheus");
            try
            {
                var results = await Task.WhenAll(queries.Select(async entry =>
                    new KeyValuePair<string, MetricValue[]>(entry.Key, await Read(client, entry.Value, ct))));
                var seconds = window switch { "24h" => 86400, "7d" => 604800, _ => 3600 };
                var end = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var url = "api/v1/query_range?query=" + Uri.EscapeDataString("histogram_quantile(0.95,sum by(le)(rate(msh_ssr_render_duration_seconds_bucket[5m])))")
                    + $"&start={end - seconds}&end={end}&step={seconds / 120}";
                using var seriesResponse = await client.GetAsync(url, ct);
                seriesResponse.EnsureSuccessStatusCode();
                using var series = await JsonDocument.ParseAsync(await seriesResponse.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                var points = series.RootElement.GetProperty("data").GetProperty("result").EnumerateArray()
                    .SelectMany(item => item.GetProperty("values").EnumerateArray()).Select(point => new {
                        timestamp = point[0].GetDouble(),
                        value = double.TryParse(point[1].GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? (double?)v : null
                    }).ToArray();
                return Results.Ok(new { state = "ready", range = window, sentryUrl = sentry, updatedAt = DateTimeOffset.UtcNow, metrics = results.ToDictionary(), series = points });
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException
                || exception is TaskCanceledException && !ct.IsCancellationRequested)
            {
                return Results.Json(new { state = "unavailable", sentryUrl = sentry, updatedAt = DateTimeOffset.UtcNow }, statusCode: 503);
            }
        }).RequireAuthorization();
    }

    private static async Task<MetricValue[]> Read(HttpClient client, string query, CancellationToken ct)
    {
        using var response = await client.GetAsync("api/v1/query?query=" + Uri.EscapeDataString(query), ct);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (json.RootElement.GetProperty("status").GetString() != "success") throw new InvalidOperationException("Prometheus query failed.");
        return json.RootElement.GetProperty("data").GetProperty("result").EnumerateArray().Select(item =>
        {
            var value = item.GetProperty("value");
            var parsed = double.TryParse(value[1].GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? (double?)number : null;
            return new MetricValue(item.GetProperty("metric").EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetString() ?? ""), parsed, value[0].GetDouble());
        }).ToArray();
    }
    public sealed record MetricValue(Dictionary<string, string> Labels, double? Value, double Timestamp);
}
