using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Infrastructure.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;

namespace MyStreamHistory.Shared.Infrastructure.Logging
{
    public static class SerilogRegistrationExtensions
    {
        private const string ConsoleOutputTemplate =
            "[{Timestamp:HH:mm:ss} {Level:u3} {TraceId}] {Message:lj}{NewLine}{Exception}";

        public static InfrastructureBuilder AddSerilog(this InfrastructureBuilder builder)
        {
            var options = builder.Configuration.GetSection("Serilog").GetValidated<SerilogOptions>();

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", options.ApplicationName);

            if (options.WriteToConsole)
            {
                // TraceId is the W3C trace id shared by every service handling the same request, including
                // RabbitMQ consumers, and matches the trace id of the Sentry event for that request.
                loggerConfig.WriteTo.Console(outputTemplate: ConsoleOutputTemplate);
            }

            if (options.WriteToElastic && !string.IsNullOrWhiteSpace(options.ElasticUrl))
            {
                loggerConfig.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(options.ElasticUrl))
                {
                    AutoRegisterTemplate = true,
                    ModifyConnectionSettings = x =>
                    {
                        if (!string.IsNullOrWhiteSpace(options.ElasticUsername))
                        {
                            x = x.BasicAuthentication(options.ElasticUsername, options.ElasticPassword);
                        }

                        return x;
                    },
                    IndexFormat = $"{options.ApplicationName.ToLower().Replace('.', '-')}-logs-DateTime.UtcNow:yyyy-MM"
                });
            }

            if (!string.IsNullOrWhiteSpace(builder.Configuration["Sentry:Dsn"]))
            {
                loggerConfig.WriteTo.Sentry(sentryOptions =>
                {
                    sentryOptions.InitializeSdk = false;
                    sentryOptions.MinimumBreadcrumbLevel = LogEventLevel.Warning;
                    sentryOptions.MinimumEventLevel = LogEventLevel.Error;
                    sentryOptions.RestrictedToMinimumLevel = LogEventLevel.Warning;
                });
            }

            Log.Logger = loggerConfig.CreateLogger();

            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.AddSerilog(dispose: true);
            });

            return builder;
        }
    }
}
