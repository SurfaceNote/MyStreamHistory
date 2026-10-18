using MyStreamHistory.ViewerService.Infrastructure.Options;

namespace MyStreamHistory.ViewerService.Api.Extensions;

public static class OptionsRegistration
{
    public static IServiceCollection AddAppOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CommunityAnalyticsOptions>()
            .Bind(configuration.GetSection(CommunityAnalyticsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<TwitchApiOptions>(configuration.GetSection("TwitchApi"));
        
        services.AddOptions<TwitchApiOptions>()
            .Bind(configuration.GetSection("TwitchApi"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        return services;
    }
}

