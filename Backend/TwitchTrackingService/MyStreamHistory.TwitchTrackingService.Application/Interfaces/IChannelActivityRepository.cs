using MyStreamHistory.TwitchTrackingService.Application.Services;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface IChannelActivityRepository
{
    /// <summary>Records one activity per channel, UTC day and kind; repeated calls only move LastSeenAt.</summary>
    Task RecordAsync(int twitchUserId, string kind, DateTime occurredAt, CancellationToken cancellationToken = default);

    Task<ChannelCollectionSnapshot> GetCollectionSnapshotAsync(int twitchUserId, CancellationToken cancellationToken = default);

    /// <summary>Sessions and daily activity for the admin product overview. Subscriptions come from the Twitch API.</summary>
    Task<ProductData> GetProductDataAsync(CancellationToken cancellationToken = default);
}

public sealed record ProductData(
    List<ProductSessionRow> Sessions,
    List<ProductActivityRow> Activity);

public sealed record ChannelCollectionSnapshot(
    int StreamsCount,
    bool IsLive,
    DateTime? LastStreamStartedAt,
    DateTime? LastStreamEndedAt,
    DateTime? LastOverlaySeenAt,
    DateTime? LastPanelSeenAt);
