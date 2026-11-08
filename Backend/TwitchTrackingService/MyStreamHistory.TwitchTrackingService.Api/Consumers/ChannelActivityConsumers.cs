using MassTransit;
using MyStreamHistory.Shared.Api.Features;
using MyStreamHistory.Shared.Base.Contracts.ProductActivity;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Application.Services;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

/// <summary>
/// Stores daily channel activity for product metrics. Runs regardless of the Twitch data
/// collection flag: it records only the owner's own product usage.
/// </summary>
public class ChannelActivityObservedConsumer(
    IChannelActivityRepository repository,
    ILogger<ChannelActivityObservedConsumer> logger) : IConsumer<ChannelActivityObservedContract>
{
    public async Task Consume(ConsumeContext<ChannelActivityObservedContract> context)
    {
        var message = context.Message;
        if (message.TwitchUserId <= 0 || !ChannelActivityKinds.All.Contains(message.Kind))
        {
            logger.LogWarning("Ignoring channel activity with kind {Kind} for channel {TwitchUserId}", message.Kind, message.TwitchUserId);
            return;
        }

        var occurredAt = message.OccurredAt == default ? DateTime.UtcNow : message.OccurredAt.ToUniversalTime();
        await repository.RecordAsync(message.TwitchUserId, message.Kind, occurredAt, context.CancellationToken);
    }
}

public class GetProductOverviewConsumer(
    IChannelActivityRepository repository,
    TwitchDataCollectionFeature dataCollection) : IConsumer<GetProductOverviewRequest>
{
    public async Task Consume(ConsumeContext<GetProductOverviewRequest> context)
    {
        var data = await repository.GetProductDataAsync(context.CancellationToken);
        await context.RespondAsync(ProductMetricsCalculator.Build(
            data.Sessions, data.Subscriptions, data.Activity, dataCollection.Enabled, DateTime.UtcNow, context.Message.Weeks));
    }
}

public class GetCollectionStatusConsumer(
    IChannelActivityRepository repository,
    TwitchDataCollectionFeature dataCollection) : IConsumer<GetCollectionStatusRequestContract>
{
    public async Task Consume(ConsumeContext<GetCollectionStatusRequestContract> context)
    {
        var snapshot = await repository.GetCollectionSnapshotAsync(context.Message.TwitchUserId, context.CancellationToken);

        await context.RespondAsync(new GetCollectionStatusResponseContract
        {
            DataCollectionEnabled = dataCollection.Enabled,
            Subscriptions = snapshot.Subscriptions
                .Select(s => new CollectionSubscriptionDto { EventType = s.EventType, Status = s.Status })
                .ToList(),
            StreamsCount = snapshot.StreamsCount,
            IsLive = snapshot.IsLive,
            LastStreamStartedAt = snapshot.LastStreamStartedAt,
            LastStreamEndedAt = snapshot.LastStreamEndedAt,
            LastOverlaySeenAt = snapshot.LastOverlaySeenAt,
            LastPanelSeenAt = snapshot.LastPanelSeenAt
        });
    }
}
