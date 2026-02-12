using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Api.BackgroundServices;

public class EventSubHistoryCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventSubHistoryCleanupBackgroundService> _logger;

    public EventSubHistoryCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<EventSubHistoryCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EventSubHistoryCleanupBackgroundService started");

        using var timer = new PeriodicTimer(TimeSpan.FromHours(2));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanupOldMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old EventSub messages");
            }
        }
    }

    private async Task CleanupOldMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IProcessedEventSubMessageRepository>();
        
        var threshold = DateTime.UtcNow.AddHours(-2);
        await repository.DeleteOlderThanAsync(threshold, cancellationToken);
        
        _logger.LogInformation("Cleaned up EventSub messages older than {Threshold}", threshold);
    }
}

