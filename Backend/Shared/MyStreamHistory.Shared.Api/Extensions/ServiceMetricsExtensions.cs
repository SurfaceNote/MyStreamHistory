using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace MyStreamHistory.Shared.Api.Extensions;

/// <summary>Small bounded-label Prometheus collector for the existing services.</summary>
public static class ServiceMetricsExtensions
{
    private static readonly double[] Bounds = [.025, .05, .1, .25, .5, 1, 2.5, 5, 10, 30, 60];
    private sealed class Row { public readonly object Gate = new(); public long Count; public double Sum; public long[] Buckets = new long[Bounds.Length]; }

    public static void UseServiceMetrics(this WebApplication app, string service)
    {
        var rows = new ConcurrentDictionary<(string Route, string Status), Row>();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/health") || context.Request.Path.StartsWithSegments("/internal")) { await next(); return; }
            var start = Stopwatch.GetTimestamp();
            try { await next(); }
            catch { if (!context.Response.HasStarted) context.Response.StatusCode = 500; throw; }
            finally
            {
                // Endpoint templates have bounded cardinality; raw IDs and query strings never become labels.
                var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
                var row = rows.GetOrAdd((route, context.Response.StatusCode / 100 + "xx"), _ => new());
                var elapsed = Stopwatch.GetElapsedTime(start).TotalSeconds;
                lock (row.Gate)
                {
                    row.Count++; row.Sum += elapsed;
                    for (var i = 0; i < Bounds.Length; i++) if (elapsed <= Bounds[i]) row.Buckets[i]++;
                }
            }
        });
        app.MapGet("/internal/metrics", (HttpContext context, IConfiguration configuration) =>
        {
            var token = configuration["Metrics:Token"];
            var supplied = Encoding.UTF8.GetBytes(context.Request.Headers.Authorization.ToString());
            var expected = Encoding.UTF8.GetBytes("Bearer " + token);
            if (string.IsNullOrWhiteSpace(token) || supplied.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(supplied, expected))
                return Results.NotFound();
            var text = new StringBuilder("# TYPE msh_http_request_duration_seconds histogram\n");
            var serviceLabel = "service=" + JsonSerializer.Serialize(service);
            foreach (var (key, row) in rows)
            {
                var labels = serviceLabel + ",route=" + JsonSerializer.Serialize(key.Route) + ",status_class=" + JsonSerializer.Serialize(key.Status);
                lock (row.Gate)
                {
                    for (var i = 0; i < Bounds.Length; i++)
                        text.AppendLine($"msh_http_request_duration_seconds_bucket{{{labels},le=\"{Bounds[i].ToString(CultureInfo.InvariantCulture)}\"}} {row.Buckets[i]}");
                    text.AppendLine($"msh_http_request_duration_seconds_bucket{{{labels},le=\"+Inf\"}} {row.Count}");
                    text.AppendLine($"msh_http_request_duration_seconds_count{{{labels}}} {row.Count}");
                    text.AppendLine($"msh_http_request_duration_seconds_sum{{{labels}}} {row.Sum.ToString(CultureInfo.InvariantCulture)}");
                }
            }
            using var process = Process.GetCurrentProcess();
            text.AppendLine($"# TYPE msh_process_resident_memory_bytes gauge\nmsh_process_resident_memory_bytes{{{serviceLabel}}} {process.WorkingSet64}");
            text.AppendLine($"# TYPE msh_process_cpu_seconds_total counter\nmsh_process_cpu_seconds_total{{{serviceLabel}}} {process.TotalProcessorTime.TotalSeconds.ToString(CultureInfo.InvariantCulture)}");
            return Results.Text(text.ToString(), "text/plain; version=0.0.4");
        }).AllowAnonymous();
    }
}
