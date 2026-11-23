using MyStreamHistory.TwitchTrackingService.Application.Services;

namespace MyStreamHistory.TwitchTrackingService.Api.BackgroundServices;

/// <summary>Runs the stream language backfill once, shortly after start.</summary>
public class StreamLanguageBackfillBackgroundService(
    IServiceProvider serviceProvider,
    ILogger<StreamLanguageBackfillBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            using var scope = serviceProvider.CreateScope();
            var backfill = scope.ServiceProvider.GetRequiredService<IStreamLanguageBackfillService>();
            await backfill.BackfillAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Stream language backfill failed; it will run again on the next start");
        }
    }
}
