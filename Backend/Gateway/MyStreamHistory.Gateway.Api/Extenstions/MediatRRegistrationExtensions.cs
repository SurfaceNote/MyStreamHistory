using MyStreamHistory.Gateway.Application.Commands;

namespace MyStreamHistory.Gateway.Api.Extenstions;

public static class MediatRRegistrationExtensions
{
    public static IServiceCollection AddMediatRHandlers(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(TwitchCallbackCommand).Assembly));
        return services;
    }
}