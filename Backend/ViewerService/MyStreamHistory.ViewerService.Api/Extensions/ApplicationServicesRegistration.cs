using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Application.Services;
using MyStreamHistory.ViewerService.Infrastructure.Persistence;
using MyStreamHistory.ViewerService.Infrastructure.Services;

namespace MyStreamHistory.ViewerService.Api.Extensions;

public static class ApplicationServicesRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IViewerRepository, ViewerRepository>();
        services.AddScoped<IViewerCategoryWatchRepository, ViewerCategoryWatchRepository>();
        services.AddScoped<IViewerStatsRepository, ViewerStatsRepository>();
        services.AddScoped<IProcessedEventSubMessageRepository, ProcessedEventSubMessageRepository>();

        // Application Services
        services.AddSingleton<IChatMessageBufferService, ChatMessageBufferService>();
        services.AddScoped<IViewerTrackingService, ViewerTrackingService>();
        services.AddScoped<IViewerDataProcessingService, ViewerDataProcessingService>();
        
        // Infrastructure Services
        services.AddHttpClient<ITwitchChatApiClient, TwitchChatApiClient>();
        services.AddHttpClient<ITwitchUsersApiClient, TwitchUsersApiClient>();
        services.AddHttpClient<ITwitchEventSubClient, TwitchEventSubClient>();
        services.AddScoped<ITwitchAppTokenService, TwitchAppTokenService>();
        services.AddScoped<IAuthTokenService, AuthTokenService>();
        services.AddScoped<IStreamCategoryService, StreamCategoryService>();

        return services;
    }
}

