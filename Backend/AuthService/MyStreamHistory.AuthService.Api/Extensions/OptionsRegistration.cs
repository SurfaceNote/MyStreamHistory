using MyStreamHistory.AuthService.Infrastructure.Options;

namespace MyStreamHistory.AuthService.Api.Extensions;

public static class OptionsRegistration
{
    public static IServiceCollection AddAppOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TwitchOptions>(configuration.GetSection(TwitchOptions.SectionName));
        
        services.AddOptions<TwitchOptions>()
            .Bind(configuration.GetSection(TwitchOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        return services;
    }
}