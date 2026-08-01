using Microsoft.EntityFrameworkCore;
using MyStreamHistory.TwitchTrackingService.Application.DTOs;
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
        return await _context.StreamSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<StreamSessionDto?> GetDtoByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.StreamSessions
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new StreamSessionDto
            {
                Id = s.Id,
                StreamId = s.StreamId,
                TwitchUserId = s.TwitchUserId,
                StreamerLogin = s.StreamerLogin,
                StreamerDisplayName = s.StreamerDisplayName,
                StreamerAvatarUrl = s.StreamerAvatarUrl,
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                IsLive = s.IsLive,
                StreamTitle = s.StreamTitle,
                GameName = s.GameName,
                ViewerCount = s.ViewerCount,
                Categories = s.StreamCategories
                    .Select(sc => new
                    {
                        sc.TwitchCategory.TwitchId,
                        sc.TwitchCategory.Name,
                        sc.TwitchCategory.BoxArtUrl,
                        sc.TwitchCategory.IgdbId
                    })
                    .Distinct()
                    .Select(c => new TwitchCategoryDto
                    {
                        TwitchId = c.TwitchId,
                        Name = c.Name,
                        BoxArtUrl = c.BoxArtUrl,
                        IgdbId = c.IgdbId
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<StreamSession>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.StreamSessions
            .AsNoTracking()
            .Where(s => s.IsLive)
            .OrderBy(s => s.TwitchUserId)
            .ToListAsync(cancellationToken);
    }

    public async Task<StreamSession?> GetActiveByTwitchUserIdAsync(
        int twitchUserId,
        CancellationToken cancellationToken = default)
    {
        return await _context.StreamSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.TwitchUserId == twitchUserId && s.IsLive,
                cancellationToken);
    }

    public async Task ConfirmActiveAsync(
        IReadOnlyCollection<Guid> streamSessionIds,
        DateTime confirmedAt,
        CancellationToken cancellationToken = default)
    {
        if (streamSessionIds.Count == 0)
        {
            return;
        }

        await _context.StreamSessions
            .Where(s => s.IsLive && streamSessionIds.Contains(s.Id))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.LastConfirmedLiveAt, confirmedAt)
                    .SetProperty(s => s.MissingSinceAt, (DateTime?)null),
                cancellationToken);
    }

    public async Task MarkMissingAsync(
        IReadOnlyCollection<Guid> streamSessionIds,
        DateTime missingSince,
        CancellationToken cancellationToken = default)
    {
        if (streamSessionIds.Count == 0)
        {
            return;
        }

        await _context.StreamSessions
            .Where(s => s.IsLive
                && streamSessionIds.Contains(s.Id)
                && s.MissingSinceAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(s => s.MissingSinceAt, missingSince),
                cancellationToken);
    }

    public async Task UpdatePollingDataAsync(
        Guid streamSessionId,
        string streamId,
        string title,
        string gameName,
        int viewerCount,
        CancellationToken cancellationToken = default)
    {
        await _context.StreamSessions
            .Where(s => s.Id == streamSessionId && s.IsLive)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.StreamId, streamId)
                    .SetProperty(s => s.StreamTitle, title)
                    .SetProperty(s => s.GameName, gameName)
                    .SetProperty(s => s.ViewerCount, viewerCount),
                cancellationToken);
    }

    public async Task<bool> TryEndActiveAsync(
        Guid streamSessionId,
        DateTime endedAt,
        CancellationToken cancellationToken = default)
    {
        var updatedRows = await _context.StreamSessions
            .Where(s => s.Id == streamSessionId && s.IsLive)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.IsLive, false)
                    .SetProperty(s => s.EndedAt, endedAt),
                cancellationToken);

        return updatedRows == 1;
    }

    public IQueryable<StreamSession> Query()
    {
        return _context.StreamSessions.AsQueryable();
    }

    public async Task<List<StreamSessionDto>> GetRecentStreamsByTwitchUserIdAsync(
        int twitchUserId,
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        return await _context.StreamSessions
            .AsNoTracking()
            .Where(s => s.TwitchUserId == twitchUserId)
            .OrderByDescending(s => s.StartedAt)
            .Take(count)
            .Select(s => new StreamSessionDto
            {
                Id = s.Id,
                StreamId = s.StreamId,
                TwitchUserId = s.TwitchUserId,
                StreamerLogin = s.StreamerLogin,
                StreamerDisplayName = s.StreamerDisplayName,
                StreamerAvatarUrl = s.StreamerAvatarUrl,
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                IsLive = s.IsLive,
                StreamTitle = s.StreamTitle,
                GameName = s.GameName,
                ViewerCount = s.ViewerCount,
                Categories = s.StreamCategories
                    .Select(sc => new
                    {
                        sc.TwitchCategory.TwitchId,
                        sc.TwitchCategory.Name,
                        sc.TwitchCategory.BoxArtUrl,
                        sc.TwitchCategory.IgdbId
                    })
                    .Distinct()
                    .Select(c => new TwitchCategoryDto
                    {
                        TwitchId = c.TwitchId,
                        Name = c.Name,
                        BoxArtUrl = c.BoxArtUrl,
                        IgdbId = c.IgdbId
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }
}

