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
        var storage = scope.ServiceProvider.GetRequiredService<MediaStorage>();
        var candidates = await db.Media.Where(x => x.CreatedAt < DateTime.UtcNow.AddDays(-7))
            .OrderBy(x => x.CreatedAt).Take(100).ToListAsync(cancellationToken);
        if (candidates.Count == 0) return;
        var draftReferences = await db.Articles.Select(x => new { x.Body, x.CoverId }).ToListAsync(cancellationToken);
        var publishedIds = await db.PublishedMedia.Select(x => x.MediaId).ToListAsync(cancellationToken);
        var published = publishedIds.ToHashSet();
        foreach (var asset in candidates)
        {
            if (published.Contains(asset.Id) || draftReferences.Any(x => x.CoverId == asset.Id || x.Body.Contains(asset.Id.ToString(), StringComparison.OrdinalIgnoreCase)))
                continue;
            await storage.DeleteAsync(asset, cancellationToken);
            db.Media.Remove(asset);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Removed unused content media {MediaId}", asset.Id);
        }
    }
}
