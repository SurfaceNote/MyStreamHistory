using MyStreamHistory.TwitchTrackingService.Application.Services;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface IChannelActivityRepository
{
    /// <summary>Records one activity per channel, UTC day and kind; repeated calls only move LastSeenAt.</summary>
    Task RecordAsync(int twitchUserId, string kind, DateTime occurredAt, CancellationToken cancellationToken = default);

    Task<ChannelCollectionSnapshot> GetCollectionSnapshotAsync(int twitchUserId, CancellationToken cancellationToken = default);

    /// <summary>All rows needed for the admin product overview (small tables: sessions, subscriptions, daily activity).</summary>
    Task<ProductData> GetProductDataAsync(CancellationToken cancellationToken = default);
}

public sealed record ProductData(
    List<ProductSessionRow> Sessions,
    List<ProductSubscriptionRow> Subscriptions,
    List<ProductActivityRow> Activity);

public sealed record ChannelSubscriptionState(string EventType, string Status);

public sealed record ChannelCollectionSnapshot(
    IReadOnlyList<ChannelSubscriptionState> Subscriptions,
    int StreamsCount,
    bool IsLive,
    DateTime? LastStreamStartedAt,
    DateTime? LastStreamEndedAt,
    DateTime? LastOverlaySeenAt,
    DateTime? LastPanelSeenAt);
