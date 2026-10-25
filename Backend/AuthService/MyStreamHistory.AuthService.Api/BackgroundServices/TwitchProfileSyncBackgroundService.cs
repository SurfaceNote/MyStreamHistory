using Microsoft.Extensions.Options;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Infrastructure.Options;
using MyStreamHistory.Shared.Api.Features;

namespace MyStreamHistory.AuthService.Api.BackgroundServices;

public sealed class TwitchProfileSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime applicationLifetime,
    IOptions<TwitchProfileSyncOptions> options,
    TimeProvider timeProvider,
    TwitchDataCollectionFeature feature,
    ILogger<TwitchProfileSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!feature.Enabled)
            return;

        try
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = applicationLifetime.ApplicationStarted.Register(() => started.TrySetResult());
            await started.Task.WaitAsync(stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.IntervalMinutes), timeProvider);
            do
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<ITwitchProfileSyncService>().SyncAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Twitch profile synchronization failed; it will retry on the next interval");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown, including shutdown before ApplicationStarted.
        }
    }
}
