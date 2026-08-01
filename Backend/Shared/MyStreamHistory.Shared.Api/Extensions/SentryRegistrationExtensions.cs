using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MyStreamHistory.Shared.Base.Exceptions;
using Sentry;
using Sentry.Extensibility;
using Sentry.Serilog;

namespace MyStreamHistory.Shared.Api.Extensions;

public static class SentryRegistrationExtensions
{
    private const double DefaultTracesSampleRate = 0.05;

    public static WebApplicationBuilder AddSentryObservability(this WebApplicationBuilder builder)
    {
        builder.WebHost.UseSentry(options =>
        {
            var configuration = builder.Configuration;
            var dsn = configuration["Sentry:Dsn"];

            if (!string.IsNullOrWhiteSpace(dsn))
            {
                options.Dsn = dsn;
            }

            options.Environment = FirstNotEmpty(
                configuration["Sentry:Environment"],
                builder.Environment.EnvironmentName.ToLowerInvariant());
            options.Release = FirstNotEmpty(
                configuration["Sentry:Release"],
                Environment.GetEnvironmentVariable("SENTRY_RELEASE"));
            options.TracesSampleRate = Math.Clamp(
                configuration.GetValue<double?>("Sentry:TracesSampleRate") ?? DefaultTracesSampleRate,
                0,
                1);
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
