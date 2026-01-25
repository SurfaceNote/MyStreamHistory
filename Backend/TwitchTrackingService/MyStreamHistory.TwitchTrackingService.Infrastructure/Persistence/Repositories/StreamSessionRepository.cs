using Microsoft.EntityFrameworkCore;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Repositories;

public class StreamSessionRepository : IStreamSessionRepository
{
    private readonly TwitchTrackingDbContext _context;

    public StreamSessionRepository(TwitchTrackingDbContext context)
    {
        _context = context;
    }

    public async Task<StreamSession> AddAsync(StreamSession entity, CancellationToken cancellationToken = default)
    {
        await _context.StreamSessions.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task<StreamSession> UpdateAsync(StreamSession entity, CancellationToken cancellationToken = default)
    {
        _context.StreamSessions.Update(entity);
        return Task.FromResult(entity);
    }

    public Task DeleteAsync(StreamSession entity, CancellationToken cancellationToken = default)
    {
        _context.StreamSessions.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<StreamSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.StreamSessions.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<List<StreamSession>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.StreamSessions.ToListAsync(cancellationToken);
    }

    public IQueryable<StreamSession> Query()
    {
        return _context.StreamSessions.AsQueryable();
    }

    public async Task<List<StreamSession>> GetRecentStreamsByTwitchUserIdAsync(int twitchUserId, int count = 10, CancellationToken cancellationToken = default)
    {
        return await _context.StreamSessions
            .Include(s => s.StreamCategories)
                .ThenInclude(sc => sc.TwitchCategory)
            .Where(s => s.TwitchUserId == twitchUserId)
            .OrderByDescending(s => s.StartedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}

