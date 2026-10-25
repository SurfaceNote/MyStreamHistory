using Microsoft.Extensions.DependencyInjection.Extensions;
using MyStreamHistory.AuthService.Api.BackgroundServices;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Infrastructure.Options;
using MyStreamHistory.AuthService.Infrastructure.Services;
using MyStreamHistory.Shared.Api.Features;

namespace MyStreamHistory.AuthService.Api.Extensions;

public static class TwitchProfileSyncRegistration
{
    public static IServiceCollection AddTwitchProfileSync(this IServiceCollection services,
        IConfiguration configuration, TwitchDataCollectionFeature feature)
    {
        services.AddOptions<TwitchProfileSyncOptions>()
            .Bind(configuration.GetSection(TwitchProfileSyncOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddHttpClient<ITwitchUsersApiClient, TwitchUsersApiClient>();
        services.AddScoped<ITwitchProfileSyncService, TwitchProfileSyncService>();

        if (feature.Enabled)
            services.AddHostedService<TwitchProfileSyncBackgroundService>();

        return services;
    }
}
