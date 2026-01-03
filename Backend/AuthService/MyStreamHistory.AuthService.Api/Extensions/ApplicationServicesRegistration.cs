using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Application.Services;
using MyStreamHistory.AuthService.Infrastructure.Persistence.Repositories;
using MyStreamHistory.AuthService.Infrastructure.Services;

namespace MyStreamHistory.AuthService.Api.Extensions;

public static class ApplicationServicesRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpClient<ITwitchAuthService, TwitchAuthService>();
        services.AddScoped<IAuthUserService, AuthUserService>();
        services.AddScoped<IAuthUserRepository, AuthUserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }
}