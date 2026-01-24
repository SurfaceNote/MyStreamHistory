using Microsoft.EntityFrameworkCore;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Repositories;

public class EventSubSubscriptionRepository : IEventSubSubscriptionRepository
{
    private readonly TwitchTrackingDbContext _context;

    public EventSubSubscriptionRepository(TwitchTrackingDbContext context)
    {
        _context = context;
    }

    public async Task<EventSubSubscription> AddAsync(EventSubSubscription entity, CancellationToken cancellationToken = default)
    {
        await _context.EventSubSubscriptions.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task<EventSubSubscription> UpdateAsync(EventSubSubscription entity, CancellationToken cancellationToken = default)
    {
        _context.EventSubSubscriptions.Update(entity);
        return Task.FromResult(entity);
    }

    public Task DeleteAsync(EventSubSubscription entity, CancellationToken cancellationToken = default)
    {
        _context.EventSubSubscriptions.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<EventSubSubscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.EventSubSubscriptions.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<List<EventSubSubscription>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.EventSubSubscriptions.ToListAsync(cancellationToken);
    }

    public IQueryable<EventSubSubscription> Query()
    {
        return _context.EventSubSubscriptions.AsQueryable();
    }
}

