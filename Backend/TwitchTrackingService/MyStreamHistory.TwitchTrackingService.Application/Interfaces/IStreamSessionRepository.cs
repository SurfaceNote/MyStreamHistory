using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface IStreamSessionRepository
{
    Task<StreamSession> AddAsync(StreamSession entity, CancellationToken cancellationToken = default);
    Task<StreamSession> UpdateAsync(StreamSession entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(StreamSession entity, CancellationToken cancellationToken = default);
    Task<StreamSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StreamSessionDto?> GetDtoByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<StreamSession>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<StreamSession?> GetActiveByTwitchUserIdAsync(int twitchUserId, CancellationToken cancellationToken = default);
    Task ConfirmActiveAsync(
        IReadOnlyCollection<Guid> streamSessionIds,
        DateTime confirmedAt,
        CancellationToken cancellationToken = default);
    Task MarkMissingAsync(
        IReadOnlyCollection<Guid> streamSessionIds,
        DateTime missingSince,
        CancellationToken cancellationToken = default);
    Task UpdatePollingDataAsync(
        Guid streamSessionId,
        string streamId,
        string title,
        string gameName,
        int viewerCount,
        CancellationToken cancellationToken = default);
    Task<bool> TryEndActiveAsync(
        Guid streamSessionId,
        DateTime endedAt,
        CancellationToken cancellationToken = default);
    IQueryable<StreamSession> Query();
    Task<List<StreamSessionDto>> GetRecentStreamsByTwitchUserIdAsync(
        int twitchUserId,
        int count = 10,
        CancellationToken cancellationToken = default);
}

