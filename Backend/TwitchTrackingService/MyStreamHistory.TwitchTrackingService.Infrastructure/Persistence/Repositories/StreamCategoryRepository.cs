using Microsoft.EntityFrameworkCore;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Repositories;

public class StreamCategoryRepository : IStreamCategoryRepository
{
    private readonly TwitchTrackingDbContext _context;

    public StreamCategoryRepository(TwitchTrackingDbContext context)
    {
        _context = context;
    }

    public async Task<StreamCategory?> GetActiveSegmentByStreamIdAsync(Guid streamSessionId, CancellationToken cancellationToken = default)
    {
        return await _context.StreamCategories
            .Include(sc => sc.TwitchCategory)
            .FirstOrDefaultAsync(sc => sc.StreamSessionId == streamSessionId && sc.EndedAt == null, cancellationToken);
    }

    public async Task<List<StreamCategory>> GetByStreamSessionIdAsync(Guid streamSessionId, CancellationToken cancellationToken = default)
    {
        return await _context.StreamCategories
            .AsNoTracking()
            .Include(sc => sc.TwitchCategory)
            .Where(sc => sc.StreamSessionId == streamSessionId)
            .OrderBy(sc => sc.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateSegmentAsync(StreamCategory segment, CancellationToken cancellationToken = default)
    {
        await _context.StreamCategories.AddAsync(segment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task CloseSegmentAsync(Guid segmentId, DateTime endedAt, CancellationToken cancellationToken = default)
    {
        var segment = await _context.StreamCategories.FindAsync(new object[] { segmentId }, cancellationToken);
        if (segment != null)
        {
            segment.EndedAt = endedAt;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateSegmentEndTimeAsync(Guid segmentId, DateTime endedAt, CancellationToken cancellationToken = default)
    {
        var segment = await _context.StreamCategories.FindAsync(new object[] { segmentId }, cancellationToken);
        if (segment != null)
        {
            segment.EndedAt = endedAt;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

