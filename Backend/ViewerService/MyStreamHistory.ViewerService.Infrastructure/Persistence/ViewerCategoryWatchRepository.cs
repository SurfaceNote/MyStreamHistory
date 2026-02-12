using Microsoft.EntityFrameworkCore;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Infrastructure.Persistence;

public class ViewerCategoryWatchRepository : IViewerCategoryWatchRepository
{
    private readonly ViewerServiceDbContext _context;

    public ViewerCategoryWatchRepository(ViewerServiceDbContext context)
    {
        _context = context;
    }

    public async Task<ViewerCategoryWatch?> GetByViewerAndCategoryAsync(Guid viewerId, Guid streamCategoryId, CancellationToken cancellationToken = default)
    {
        return await _context.ViewerCategoryWatches
            .FirstOrDefaultAsync(w => w.ViewerId == viewerId && w.StreamCategoryId == streamCategoryId, cancellationToken);
    }

    public async Task<List<ViewerCategoryWatch>> GetByViewerIdAsync(Guid viewerId, CancellationToken cancellationToken = default)
    {
        return await _context.ViewerCategoryWatches
            .Where(w => w.ViewerId == viewerId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ViewerCategoryWatch>> GetByStreamCategoryIdAsync(Guid streamCategoryId, CancellationToken cancellationToken = default)
    {
        return await _context.ViewerCategoryWatches
            .Include(w => w.Viewer)
            .Where(w => w.StreamCategoryId == streamCategoryId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ViewerCategoryWatch>> GetByStreamCategoryIdsAsync(List<Guid> streamCategoryIds, CancellationToken cancellationToken = default)
    {
        return await _context.ViewerCategoryWatches
            .Include(w => w.Viewer)
            .Where(w => streamCategoryIds.Contains(w.StreamCategoryId))
            .OrderByDescending(w => w.MinutesWatched)
            .ToListAsync(cancellationToken);
    }

    public async Task BulkUpsertAsync(List<ViewerCategoryWatch> watches, CancellationToken cancellationToken = default)
    {
        foreach (var watch in watches)
        {
            var existing = await _context.ViewerCategoryWatches
                .FirstOrDefaultAsync(w => w.ViewerId == watch.ViewerId && w.StreamCategoryId == watch.StreamCategoryId, cancellationToken);

            if (existing != null)
            {
                existing.MinutesWatched = watch.MinutesWatched;
                existing.ChatPoints = watch.ChatPoints;
                existing.LastUpdatedAt = watch.LastUpdatedAt;
            }
            else
            {
                _context.ViewerCategoryWatches.Add(watch);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

