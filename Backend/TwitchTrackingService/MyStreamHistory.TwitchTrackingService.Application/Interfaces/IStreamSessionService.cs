using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface IStreamSessionService
{
    Task HandleStreamOnlineAsync(StreamOnlineEventDto eventDto, CancellationToken cancellationToken = default);
    Task HandleStreamOfflineAsync(StreamOfflineEventDto eventDto, CancellationToken cancellationToken = default);
    Task ReconcileActiveStreamsAsync(
        IReadOnlyCollection<StreamSession> activeSessions,
        IReadOnlyCollection<TwitchStreamDto> streams,
        IReadOnlyCollection<TrackedUserProfileDto> trackedUsers,
        DateTime checkedAt,
        CancellationToken cancellationToken = default);
    Task<List<StreamSessionDto>> GetRecentStreamsByTwitchUserIdAsync(int twitchUserId, int count = 10, CancellationToken cancellationToken = default);
    Task<StreamSessionDto?> GetStreamSessionByIdAsync(Guid streamSessionId, CancellationToken cancellationToken = default);
}

