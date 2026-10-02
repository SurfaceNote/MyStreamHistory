using MyStreamHistory.Shared.Api.Features;
using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class StreamLiveConfirmationLostConsumer(
    TwitchDataCollectionFeature dataCollection,
    IChatMessageBufferService bufferService,
    ILogger<StreamLiveConfirmationLostConsumer> logger)
    : IConsumer<StreamLiveConfirmationLostEventContract>
{
    public Task Consume(ConsumeContext<StreamLiveConfirmationLostEventContract> context)
    {
        if (!dataCollection.Enabled) return Task.CompletedTask;

        var message = context.Message;
        bufferService.PauseStream(message.BroadcasterUserId.ToString(), message.StreamSessionId);
        logger.LogInformation(
            "Paused viewer accrual for stream {StreamSessionId}; live confirmation missing since {MissingSince}",
            message.StreamSessionId,
            message.MissingSince);
        return Task.CompletedTask;
    }
}
