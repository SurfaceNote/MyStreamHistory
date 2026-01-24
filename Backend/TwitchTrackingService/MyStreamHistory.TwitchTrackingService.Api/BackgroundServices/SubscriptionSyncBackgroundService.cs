using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Api.BackgroundServices;

public class SubscriptionSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SubscriptionSyncBackgroundService> _logger;
    private readonly TimeSpan _syncInterval = TimeSpan.FromHours(1); // Sync every hour

    public SubscriptionSyncBackgroundService(IServiceProvider serviceProvider, ILogger<SubscriptionSyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SubscriptionSyncBackgroundService is starting");

        // Wait a bit before first sync to let the application start properly
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting EventSub subscriptions sync");

                using var scope = _serviceProvider.CreateScope();
                var subscriptionSyncService = scope.ServiceProvider.GetRequiredService<ISubscriptionSyncService>();
                
                await subscriptionSyncService.SyncSubscriptionsAsync(stoppingToken);

                _logger.LogInformation("EventSub subscriptions sync completed. Next sync in {Interval}", _syncInterval);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during EventSub subscriptions sync");
            }

            await Task.Delay(_syncInterval, stoppingToken);
        }

        _logger.LogInformation("SubscriptionSyncBackgroundService is stopping");
    }
}

