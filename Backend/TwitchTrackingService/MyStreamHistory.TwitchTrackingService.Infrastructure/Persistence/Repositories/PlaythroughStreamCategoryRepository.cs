using Microsoft.EntityFrameworkCore;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Repositories;

public class PlaythroughStreamCategoryRepository(TwitchTrackingDbContext context) : IPlaythroughStreamCategoryRepository
{
    public async Task<List<PlaythroughStreamCategory>> GetByPlaythroughIdAsync(Guid playthroughId, CancellationToken cancellationToken = default)
    {
        return await context.PlaythroughStreamCategories
            .Where(x => x.PlaythroughId == playthroughId)
            .ToListAsync(cancellationToken);
    }

    public async Task<HashSet<Guid>> GetAssignedStreamCategoryIdsAsync(CancellationToken cancellationToken = default)
    {
        return await context.PlaythroughStreamCategories
            .Select(x => x.StreamCategoryId)
            .ToHashSetAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid playthroughId, Guid streamCategoryId, CancellationToken cancellationToken = default)
    {
        return context.PlaythroughStreamCategories
            .AnyAsync(x => x.PlaythroughId == playthroughId && x.StreamCategoryId == streamCategoryId, cancellationToken);
    }

    public async Task AddRangeAsync(List<PlaythroughStreamCategory> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
        {
            return;
        }

        await context.PlaythroughStreamCategories.AddRangeAsync(entities, cancellationToken);
    }

    public Task RemoveRangeAsync(List<PlaythroughStreamCategory> entities, CancellationToken cancellationToken = default)
    {
        if (entities.Count == 0)
        {
            return Task.CompletedTask;
        }

        context.PlaythroughStreamCategories.RemoveRange(entities);
        return Task.CompletedTask;
    }

    public IQueryable<PlaythroughStreamCategory> Query()
    {
        return context.PlaythroughStreamCategories.AsQueryable();
    }
}
