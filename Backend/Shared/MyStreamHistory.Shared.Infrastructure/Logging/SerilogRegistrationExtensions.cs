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
        public static InfrastructureBuilder AddSerilog(this InfrastructureBuilder builder)
        {
            var serilogOptions = builder.Services.BuildServiceProvider();
            var options = builder.Configuration.GetSection("Serilog").GetValidated<SerilogOptions>();

            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", options.ApplicationName);

            if (options.WriteToConsole)
            {
                loggerConfig.WriteTo.Console();
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
