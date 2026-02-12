using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Infrastructure.Persistence;

public class ViewerRepository : IViewerRepository
{
    private readonly ViewerServiceDbContext _context;
    private readonly ILogger<ViewerRepository> _logger;

    public ViewerRepository(ViewerServiceDbContext context, ILogger<ViewerRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Viewer?> GetByTwitchUserIdAsync(string twitchUserId, CancellationToken cancellationToken = default)
    {
        return await _context.Viewers
            .FirstOrDefaultAsync(v => v.TwitchUserId == twitchUserId, cancellationToken);
    }

    public async Task<Viewer> AddAsync(Viewer viewer, CancellationToken cancellationToken = default)
    {
        _context.Viewers.Add(viewer);
        await _context.SaveChangesAsync(cancellationToken);
        return viewer;
    }

    public async Task<List<Viewer>> GetOrCreateViewersAsync(List<(string TwitchUserId, string Login, string DisplayName)> viewers, CancellationToken cancellationToken = default)
    {
        var twitchUserIds = viewers.Select(v => v.TwitchUserId).Distinct().ToList();
        
        // Get existing viewers
        var existingViewers = await _context.Viewers
            .Where(v => twitchUserIds.Contains(v.TwitchUserId))
            .ToListAsync(cancellationToken);

        var existingIds = existingViewers.Select(v => v.TwitchUserId).ToHashSet();
        
        // Prepare new viewers to insert (deduplicate by TwitchUserId)
        var newViewers = viewers
            .Where(v => !existingIds.Contains(v.TwitchUserId))
            .GroupBy(v => v.TwitchUserId)
            .Select(g => g.First())
            .Select(v => new Viewer
            {
                Id = Guid.NewGuid(),
                TwitchUserId = v.TwitchUserId,
                Login = v.Login,
                DisplayName = v.DisplayName,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        if (newViewers.Any())
        {
            // Insert viewers one by one to handle race conditions gracefully
            // If a viewer already exists (created by another thread), skip it
            var insertedCount = 0;
            
            foreach (var viewer in newViewers)
            {
                try
                {
                    _context.Viewers.Add(viewer);
                    await _context.SaveChangesAsync(cancellationToken);
                    insertedCount++;
                }
                catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
                {
                    // Unique constraint violation - viewer already exists (race condition)
                    // Detach the entity and continue
                    _context.Entry(viewer).State = EntityState.Detached;
                    _logger.LogDebug("Viewer {TwitchUserId} already exists, skipping", viewer.TwitchUserId);
                }
                catch (Exception ex)
                {
                    // Other errors - log and detach
                    _context.Entry(viewer).State = EntityState.Detached;
                    _logger.LogError(ex, "Error inserting viewer {TwitchUserId}", viewer.TwitchUserId);
                }
            }
            
            _logger.LogDebug("Inserted {InsertedCount} new viewers out of {TotalCount} candidates", 
                insertedCount, newViewers.Count);
            
            // Clear any remaining tracked entities to get fresh data
            _context.ChangeTracker.Clear();
            
            // Fetch all viewers from database (existing + newly created by ANY thread)
            var allViewers = await _context.Viewers
                .AsNoTracking()
                .Where(v => twitchUserIds.Contains(v.TwitchUserId))
                .ToListAsync(cancellationToken);
            
            _logger.LogDebug("Retrieved {Count} total viewers after insert", allViewers.Count);
            
            return allViewers;
        }

        return existingViewers;
    }

    public async Task UpdateAsync(Viewer viewer, CancellationToken cancellationToken = default)
    {
        _context.Viewers.Update(viewer);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BulkUpdateAsync(List<Viewer> viewers, CancellationToken cancellationToken = default)
    {
        if (!viewers.Any())
        {
            return;
        }

        _context.Viewers.UpdateRange(viewers);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

