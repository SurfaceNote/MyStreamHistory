using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyStreamHistory.Shared.Base.Exceptions;
using OpenTelemetry.Trace;
using Sentry;
using Sentry.Extensibility;
using Sentry.OpenTelemetry;
using Sentry.Serilog;

namespace MyStreamHistory.Shared.Api.Extensions;

public static class SentryRegistrationExtensions
{
    private const double DefaultTracesSampleRate = 0.05;

    /// <summary>Activity sources traced in addition to ASP.NET Core and HttpClient.</summary>
    internal static readonly string[] TracedSources = ["MassTransit", "Npgsql"];

    public static WebApplicationBuilder AddSentryObservability(this WebApplicationBuilder builder)
    {
        var tracesSampleRate = Math.Clamp(
            builder.Configuration.GetValue<double?>("Sentry:TracesSampleRate") ?? DefaultTracesSampleRate,
            0,
            1);

        // OpenTelemetry carries the trace across services, including RabbitMQ: MassTransit writes the
        // caller's trace context into message headers and continues it in the consumer. Sentry receives
        // the recorded spans, so one user action shows up as a single trace from Gateway to the consumer.
        builder.Services.AddOpenTelemetry().WithTracing(tracing => tracing
            .SetSampler(CreateSampler(tracesSampleRate))
            .AddAspNetCoreInstrumentation(instrumentation =>
                instrumentation.Filter = context => !IsInfrastructurePath(context.Request.Path))
            .AddHttpClientInstrumentation()
            .AddSource(TracedSources)
            .AddSentry());

        builder.WebHost.UseSentry(options =>
        {
            var configuration = builder.Configuration;
            var dsn = FirstNotEmpty(
                configuration["Sentry:Dsn"],
                Environment.GetEnvironmentVariable("SENTRY_DSN"));

            // Sentry treats a missing DSN as a configuration error. An empty DSN
            // explicitly disables the SDK for local environments.
            options.Dsn = dsn ?? string.Empty;

            options.Environment = FirstNotEmpty(
                configuration["Sentry:Environment"],
                builder.Environment.EnvironmentName.ToLowerInvariant());
            options.Release = FirstNotEmpty(
                configuration["Sentry:Release"],
                Environment.GetEnvironmentVariable("SENTRY_RELEASE"));
            // The sampling decision is made once per trace by the OpenTelemetry sampler and travels with
            // the trace context, so every service keeps or drops the same traces. Sentry only receives
            // spans that were already sampled and must keep all of them.
            options.TracesSampleRate = tracesSampleRate > 0 ? 1.0 : 0.0;
            options.UseOpenTelemetry(disableSentryTracing: true);
            options.SendDefaultPii = false;
            options.AttachStacktrace = true;
            options.MaxRequestBodySize = RequestSize.None;
            options.Debug = configuration.GetValue<bool>("Sentry:Debug");
            options.DiagnosticLevel = SentryLevel.Warning;
            options.DefaultTags["service"] = builder.Environment.ApplicationName;

            // The ASP.NET SDK owns initialization; the Serilog sink only forwards events
            // and enriches them with the current logging scope.
            options.ApplySerilogScopeToEvents();

            options.SetBeforeSend(sentryEvent =>
            {
                if (IsExpectedException(sentryEvent.Exception))
                {
                    return null!;
                }

                // Keep the free error quota for actionable failures. Structured error logs
                // without an exception remain in the normal Serilog destinations.
                if (sentryEvent.Exception is null && sentryEvent.Level != SentryLevel.Fatal)
                {
                    return null!;
                }

                return sentryEvent;
            });

            options.SetBeforeSendTransaction(transaction =>
                transaction.Name.Contains("/health", StringComparison.OrdinalIgnoreCase)
                    ? null!
                    : transaction);
        });

        return builder;
    }

    internal static Sampler CreateSampler(double tracesSampleRate) =>
        new ParentBasedSampler(new OperationRootSampler(new TraceIdRatioBasedSampler(tracesSampleRate)));

    private static bool IsInfrastructurePath(PathString path) =>
        path.StartsWithSegments("/health") || path.StartsWithSegments("/internal");

    /// <summary>
    /// Starts traces only for operations: incoming requests and bus messages. A database query or an
    /// outgoing HTTP call made outside any operation (for example, inside a background polling loop)
    /// would otherwise become a separate Sentry transaction on every iteration.
    /// </summary>
    private sealed class OperationRootSampler(Sampler inner) : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) =>
            samplingParameters.Kind == ActivityKind.Client
                ? new SamplingResult(SamplingDecision.Drop)
                : inner.ShouldSample(samplingParameters);
    }

    private static bool IsExpectedException(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is OperationCanceledException or AppException)
            {
                return true;
            }

            if (current.GetType().FullName == "FluentValidation.ValidationException")
            {
                return true;
            }
        }

        return false;
    }

    private static string? FirstNotEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
