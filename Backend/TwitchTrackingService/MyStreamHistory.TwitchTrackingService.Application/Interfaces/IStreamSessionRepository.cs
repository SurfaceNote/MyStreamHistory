using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface IStreamSessionRepository
{
    Task<StreamSession> AddAsync(StreamSession entity, CancellationToken cancellationToken = default);
    Task<StreamSession> UpdateAsync(StreamSession entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(StreamSession entity, CancellationToken cancellationToken = default);
    Task<StreamSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<StreamSession>> GetAllAsync(CancellationToken cancellationToken = default);
    IQueryable<StreamSession> Query();
    Task<List<StreamSession>> GetRecentStreamsByTwitchUserIdAsync(int twitchUserId, int count = 10, CancellationToken cancellationToken = default);
}

