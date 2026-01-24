using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Application.Services;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Options;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Repositories;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Services;

namespace MyStreamHistory.TwitchTrackingService.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddTwitchTrackingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Add DbContext
        services.AddDbContext<TwitchTrackingDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString);
        });

        // Add Options
        services.AddOptions<TwitchApiOptions>()
            .Bind(configuration.GetSection(TwitchApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Add HttpClient for Twitch API
        // Register as Singleton to maintain token cache across requests
        services.AddHttpClient<TwitchApiClient>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));
        
        services.AddSingleton<ITwitchApiClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(nameof(TwitchApiClient));
            var options = sp.GetRequiredService<IOptions<TwitchApiOptions>>();
            var logger = sp.GetRequiredService<ILogger<TwitchApiClient>>();
            return new TwitchApiClient(httpClient, options, logger);
        });

        // Add Repositories
        services.AddScoped<IStreamSessionRepository, StreamSessionRepository>();
        services.AddScoped<IEventSubSubscriptionRepository, EventSubSubscriptionRepository>();

        // Add Application Services
        services.AddScoped<IStreamSessionService, StreamSessionService>();
        services.AddScoped<ISubscriptionSyncService, SubscriptionSyncService>();

        return services;
    }
}

