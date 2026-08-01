using System.Diagnostics;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using Sentry;

namespace MyStreamHistory.TwitchTrackingService.Api.BackgroundServices;

public class StreamDataPollingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StreamDataPollingBackgroundService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMinutes(1);
    private readonly string _sentryMonitorSlug;

    public StreamDataPollingBackgroundService(
        IServiceProvider serviceProvider, 
        ILogger<StreamDataPollingBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _sentryMonitorSlug = configuration["Sentry:CronMonitorSlug"] ?? "twitch-stream-polling";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StreamDataPollingBackgroundService is starting");

        // Wait a bit before first poll to let the application start properly
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var checkInId = SentrySdk.CaptureCheckIn(
                _sentryMonitorSlug,
                CheckInStatus.InProgress,
                configureMonitorOptions: monitorOptions =>
                {
                    monitorOptions.Interval(1, SentryMonitorInterval.Minute);
                    monitorOptions.CheckInMargin = TimeSpan.FromMinutes(3);
                    monitorOptions.MaxRuntime = TimeSpan.FromMinutes(2);
                    monitorOptions.FailureIssueThreshold = 2;
                    monitorOptions.RecoveryThreshold = 1;
                });
            var checkInStopwatch = Stopwatch.StartNew();
            var checkInStatus = CheckInStatus.Ok;

            try
            {
                _logger.LogInformation("Starting stream data polling");

                using var scope = _serviceProvider.CreateScope();
                var streamSessionRepository = scope.ServiceProvider.GetRequiredService<IStreamSessionRepository>();
                var twitchApiClient = scope.ServiceProvider.GetRequiredService<ITwitchApiClient>();
                var streamSessionService = scope.ServiceProvider.GetRequiredService<IStreamSessionService>();
                var categoryTrackingService = scope.ServiceProvider.GetRequiredService<ICategoryTrackingService>();
                var userProfileService = scope.ServiceProvider.GetRequiredService<IUserProfileService>();

                var activeSessions = await streamSessionRepository.GetActiveAsync(stoppingToken);
                var trackedUsers = await userProfileService.GetTrackedUsersAsync(stoppingToken);
                var reconciliationUsers = trackedUsers ?? Array.Empty<TrackedUserProfileDto>();
                var userIds = activeSessions
                    .Select(session => session.TwitchUserId)
                    .Concat(reconciliationUsers.Select(user => user.TwitchUserId))
                    .Distinct()
                    .ToList();

                if (userIds.Count == 0)
                {
                    _logger.LogDebug("No active sessions or tracked users to poll");
                }
                else
                {
                    _logger.LogInformation(
                        "Polling Twitch for {TrackedUserCount} users ({ActiveStreamCount} locally active)",
                        userIds.Count,
                        activeSessions.Count);

                    // Fetch current stream data from Twitch API (will be split into batches of 100)
                    var streams = await twitchApiClient.GetStreamsAsync(userIds, stoppingToken);

                    var checkedAt = DateTime.UtcNow;
                    await streamSessionService.ReconcileActiveStreamsAsync(
                        activeSessions,
                        streams,
                        reconciliationUsers,
                        checkedAt,
                        stoppingToken);

                    // Process categories for streams
                    try
                    {
                        // Create dictionary: StreamSessionId -> GameId
                        var streamGameIds = new Dictionary<Guid, string>();
                        var currentActiveSessions = await streamSessionRepository.GetActiveAsync(stoppingToken);
                        var sessionsByUserId = currentActiveSessions.ToDictionary(s => s.TwitchUserId);
                        
                        foreach (var stream in streams)
                        {
                            if (!int.TryParse(stream.UserId, out var userId))
                            {
                                continue;
                            }

                            if (sessionsByUserId.TryGetValue(userId, out var session)
                                && !string.IsNullOrWhiteSpace(stream.GameId))
                            {
                                streamGameIds[session.Id] = stream.GameId;
                            }
                        }

                        if (streamGameIds.Count > 0)
                        {
                            await categoryTrackingService.ProcessStreamCategoriesAsync(streamGameIds, stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        checkInStatus = CheckInStatus.Error;
                        _logger.LogError(ex, "Error processing stream categories. Continuing with next poll cycle.");
                    }

                    _logger.LogInformation("Stream data polling completed. Next poll in {Interval}", _pollingInterval);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                checkInStatus = CheckInStatus.Error;
                _logger.LogError(ex, "Error during stream data polling");
            }
            finally
            {
                checkInStopwatch.Stop();

                if (!stoppingToken.IsCancellationRequested)
                {
                    SentrySdk.CaptureCheckIn(
                        _sentryMonitorSlug,
                        checkInStatus,
                        checkInId,
                        checkInStopwatch.Elapsed);
                }
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("StreamDataPollingBackgroundService is stopping");
    }
}

