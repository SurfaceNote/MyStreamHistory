using MyStreamHistory.TwitchTrackingService.Application.DTOs;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface IStreamSessionService
{
    Task HandleStreamOnlineAsync(StreamOnlineEventDto eventDto, CancellationToken cancellationToken = default);
    Task HandleStreamOfflineAsync(StreamOfflineEventDto eventDto, CancellationToken cancellationToken = default);
    Task UpdateActiveStreamsDataAsync(List<TwitchStreamDto> streams, CancellationToken cancellationToken = default);
    Task<List<StreamSessionDto>> GetRecentStreamsByTwitchUserIdAsync(int twitchUserId, int count = 10, CancellationToken cancellationToken = default);
    Task<StreamSessionDto?> GetStreamSessionByIdAsync(Guid streamSessionId, CancellationToken cancellationToken = default);
}

