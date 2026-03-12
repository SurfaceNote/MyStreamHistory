using Microsoft.EntityFrameworkCore;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Repositories;

public class PlaythroughRepository(TwitchTrackingDbContext context) : IPlaythroughRepository
{
    public async Task<Playthrough?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Playthroughs
            .Include(p => p.TwitchCategory)
            .Include(p => p.PlaythroughStreamCategories)
                .ThenInclude(psc => psc.StreamCategory)
                    .ThenInclude(sc => sc.StreamSession)
            .Include(p => p.PlaythroughStreamCategories)
                .ThenInclude(psc => psc.StreamCategory)
                    .ThenInclude(sc => sc.TwitchCategory)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<List<Playthrough>> GetByTwitchUserIdAsync(int twitchUserId, CancellationToken cancellationToken = default)
    {
        return await context.Playthroughs
            .AsNoTracking()
            .Include(p => p.TwitchCategory)
            .Include(p => p.PlaythroughStreamCategories)
                .ThenInclude(psc => psc.StreamCategory)
                    .ThenInclude(sc => sc.StreamSession)
            .Include(p => p.PlaythroughStreamCategories)
                .ThenInclude(psc => psc.StreamCategory)
                    .ThenInclude(sc => sc.TwitchCategory)
            .Where(p => p.TwitchUserId == twitchUserId)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid?> GetAutoAddPlaythroughIdAsync(int twitchUserId, Guid twitchCategoryId, CancellationToken cancellationToken = default)
    {
        return await context.Playthroughs
            .Where(p => p.TwitchUserId == twitchUserId
                && p.TwitchCategoryId == twitchCategoryId
                && p.AutoAddNewStreams)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> HasConflictingAutoAddAsync(int twitchUserId, Guid twitchCategoryId, Guid? excludedPlaythroughId, CancellationToken cancellationToken = default)
    {
        return await context.Playthroughs
            .Where(p => p.TwitchUserId == twitchUserId
                && p.TwitchCategoryId == twitchCategoryId
                && p.AutoAddNewStreams
                && (!excludedPlaythroughId.HasValue || p.Id != excludedPlaythroughId.Value))
            .AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Playthrough playthrough, CancellationToken cancellationToken = default)
    {
        await context.Playthroughs.AddAsync(playthrough, cancellationToken);
    }

    public Task UpdateAsync(Playthrough playthrough, CancellationToken cancellationToken = default)
    {
        context.Playthroughs.Update(playthrough);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Playthrough playthrough, CancellationToken cancellationToken = default)
    {
        context.Playthroughs.Remove(playthrough);
        return Task.CompletedTask;
    }

    public IQueryable<Playthrough> Query()
    {
        return context.Playthroughs.AsQueryable();
    }
}
