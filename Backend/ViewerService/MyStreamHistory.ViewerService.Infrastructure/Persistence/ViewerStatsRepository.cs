using Microsoft.EntityFrameworkCore;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Infrastructure.Persistence;

public class ViewerStatsRepository : IViewerStatsRepository
{
    private readonly ViewerServiceDbContext _context;

    public ViewerStatsRepository(ViewerServiceDbContext context)
    {
        _context = context;
    }

    public async Task<ViewerStats?> GetByViewerAndStreamerAsync(Guid viewerId, string streamerTwitchUserId, CancellationToken cancellationToken = default)
    {
        return await _context.ViewerStats
            .FirstOrDefaultAsync(s => s.ViewerId == viewerId && s.StreamerTwitchUserId == streamerTwitchUserId, cancellationToken);
    }

    public async Task<List<ViewerStats>> GetTopViewersByStreamerAsync(string streamerTwitchUserId, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _context.ViewerStats
            .Include(s => s.Viewer)
            .Where(s => s.StreamerTwitchUserId == streamerTwitchUserId)
            .OrderByDescending(s => s.Experience)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ViewerStats>> GetByViewerIdAsync(Guid viewerId, CancellationToken cancellationToken = default)
    {
        return await _context.ViewerStats
            .Where(s => s.ViewerId == viewerId)
            .ToListAsync(cancellationToken);
    }

    public async Task BulkUpsertAsync(List<ViewerStats> stats, CancellationToken cancellationToken = default)
    {
        foreach (var stat in stats)
        {
            var existing = await _context.ViewerStats
                .FirstOrDefaultAsync(s => s.ViewerId == stat.ViewerId && s.StreamerTwitchUserId == stat.StreamerTwitchUserId, cancellationToken);

            if (existing != null)
            {
                existing.MinutesWatched = stat.MinutesWatched;
                existing.EarnedMsgPoints = stat.EarnedMsgPoints;
                existing.Experience = stat.Experience;
                existing.LastUpdatedAt = stat.LastUpdatedAt;
            }
            else
            {
                _context.ViewerStats.Add(stat);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}

