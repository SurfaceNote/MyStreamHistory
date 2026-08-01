using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class StreamLiveConfirmationRestoredConsumer(
    IChatMessageBufferService bufferService,
    ILogger<StreamLiveConfirmationRestoredConsumer> logger)
    : IConsumer<StreamLiveConfirmationRestoredEventContract>
{
    public Task Consume(ConsumeContext<StreamLiveConfirmationRestoredEventContract> context)
    {
        var message = context.Message;
        bufferService.ResumeStream(message.BroadcasterUserId.ToString(), message.StreamSessionId);
        logger.LogDebug(
            "Resumed viewer accrual for confirmed stream {StreamSessionId} at {ConfirmedAt}",
            message.StreamSessionId,
            message.ConfirmedAt);
        return Task.CompletedTask;
    }
}
