using Microsoft.EntityFrameworkCore;

namespace MyStreamHistory.ContentService.Api;

public sealed class MediaCleanupService(IServiceScopeFactory scopes, ILogger<MediaCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(12));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await Sweep(stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Could not clean unused content media");
            }
        }
    }

    private async Task Sweep(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        var deletion = scope.ServiceProvider.GetRequiredService<MediaDeletion>();
        var candidates = await db.Media.AsNoTracking().Where(x => x.CreatedAt < DateTime.UtcNow.AddDays(-7))
            .OrderBy(x => x.CreatedAt).Select(x => x.Id).Take(100).ToListAsync(cancellationToken);
        if (candidates.Count == 0) return;
        foreach (var id in candidates)
        {
            if (await deletion.DeleteAsync(id, cancellationToken) == MediaDeleteResult.Deleted)
                logger.LogInformation("Removed unused content media {MediaId}", id);
        }
    }
}
