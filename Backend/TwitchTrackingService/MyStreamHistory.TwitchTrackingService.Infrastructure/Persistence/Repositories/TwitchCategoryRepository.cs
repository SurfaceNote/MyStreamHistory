using Microsoft.EntityFrameworkCore;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Repositories;

public class TwitchCategoryRepository : ITwitchCategoryRepository
{
    private readonly TwitchTrackingDbContext _context;

    public TwitchCategoryRepository(TwitchTrackingDbContext context)
    {
        _context = context;
    }

    public async Task<List<TwitchCategory>> GetByTwitchIdsAsync(List<string> twitchIds, CancellationToken cancellationToken = default)
    {
        return await _context.TwitchCategories
            .AsNoTracking()
            .Where(c => twitchIds.Contains(c.TwitchId))
            .ToListAsync(cancellationToken);
    }

    public async Task<TwitchCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.TwitchCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task AddRangeAsync(List<TwitchCategory> categories, CancellationToken cancellationToken = default)
    {
        await _context.TwitchCategories.AddRangeAsync(categories, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(List<TwitchCategory> categories, CancellationToken cancellationToken = default)
    {
        _context.TwitchCategories.UpdateRange(categories);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

