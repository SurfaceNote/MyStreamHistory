using MyStreamHistory.Shared.Api.Features;
using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class StreamLiveConfirmationRestoredConsumer(
    TwitchDataCollectionFeature dataCollection,
    IChatMessageBufferService bufferService,
    ILogger<StreamLiveConfirmationRestoredConsumer> logger)
    : IConsumer<StreamLiveConfirmationRestoredEventContract>
{
    public Task Consume(ConsumeContext<StreamLiveConfirmationRestoredEventContract> context)
    {
        if (!dataCollection.Enabled) return Task.CompletedTask;

        var message = context.Message;
        bufferService.ResumeStream(message.BroadcasterUserId.ToString(), message.StreamSessionId);
        logger.LogDebug(
            "Resumed viewer accrual for confirmed stream {StreamSessionId} at {ConfirmedAt}",
            message.StreamSessionId,
            message.ConfirmedAt);
        return Task.CompletedTask;
    }
}
