using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddHttpClient<ITwitchApiClient, TwitchApiClient>();

        // Add Repositories
        services.AddScoped<IStreamSessionRepository, StreamSessionRepository>();
        services.AddScoped<IEventSubSubscriptionRepository, EventSubSubscriptionRepository>();

        // Add Application Services
        services.AddScoped<IStreamSessionService, StreamSessionService>();
        services.AddScoped<ISubscriptionSyncService, SubscriptionSyncService>();

        return services;
    }
}

