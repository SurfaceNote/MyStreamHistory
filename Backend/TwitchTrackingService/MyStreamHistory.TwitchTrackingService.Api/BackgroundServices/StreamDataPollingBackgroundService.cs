using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Api.BackgroundServices;

public class StreamDataPollingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StreamDataPollingBackgroundService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(1);

    public StreamDataPollingBackgroundService(
        IServiceProvider serviceProvider, 
        ILogger<StreamDataPollingBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StreamDataPollingBackgroundService is starting");

        // Wait a bit before first poll to let the application start properly
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting stream data polling");

                using var scope = _serviceProvider.CreateScope();
                var streamSessionRepository = scope.ServiceProvider.GetRequiredService<IStreamSessionRepository>();
                var twitchApiClient = scope.ServiceProvider.GetRequiredService<ITwitchApiClient>();
                var streamSessionService = scope.ServiceProvider.GetRequiredService<IStreamSessionService>();

                // Get all active stream sessions
                var allSessions = await streamSessionRepository.GetAllAsync(stoppingToken);
                var activeSessions = allSessions.Where(s => s.IsLive).ToList();

                if (activeSessions.Count == 0)
                {
                    _logger.LogDebug("No active streams to poll");
                }
                else
                {
                    _logger.LogInformation("Polling data for {ActiveStreamCount} active streams", activeSessions.Count);

                    // Get list of Twitch user IDs
                    var userIds = activeSessions.Select(s => s.TwitchUserId).ToList();

                    // Fetch current stream data from Twitch API (will be split into batches of 100)
                    var streams = await twitchApiClient.GetStreamsAsync(userIds, stoppingToken);

                    // Update active stream sessions with fresh data
                    await streamSessionService.UpdateActiveStreamsDataAsync(streams, stoppingToken);

                    _logger.LogInformation("Stream data polling completed. Next poll in {Interval}", _pollingInterval);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during stream data polling");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("StreamDataPollingBackgroundService is stopping");
    }
}

