using MyStreamHistory.Gateway.Api.Mapping;

namespace MyStreamHistory.Gateway.Api.Extenstions;

public static class AutoMapperRegistrationExtensions
{
    public static IServiceCollection AddAutoMapperProfiles(this IServiceCollection services)
    {
        services.AddAutoMapper(typeof(ApiToApplicationProfile));
        
        return services;
    }
}