using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface IStreamCategoryRepository
{
    Task<StreamCategory?> GetActiveSegmentByStreamIdAsync(Guid streamSessionId, CancellationToken cancellationToken = default);
    Task<List<StreamCategory>> GetByStreamSessionIdAsync(Guid streamSessionId, CancellationToken cancellationToken = default);
    Task CreateSegmentAsync(StreamCategory segment, CancellationToken cancellationToken = default);
    Task CloseSegmentAsync(Guid segmentId, DateTime endedAt, CancellationToken cancellationToken = default);
    Task UpdateSegmentEndTimeAsync(Guid segmentId, DateTime endedAt, CancellationToken cancellationToken = default);
}

