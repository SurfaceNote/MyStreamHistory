using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class StreamLiveConfirmationLostConsumer(
    IChatMessageBufferService bufferService,
    ILogger<StreamLiveConfirmationLostConsumer> logger)
    : IConsumer<StreamLiveConfirmationLostEventContract>
{
    public Task Consume(ConsumeContext<StreamLiveConfirmationLostEventContract> context)
    {
        var message = context.Message;
        bufferService.PauseStream(message.BroadcasterUserId.ToString(), message.StreamSessionId);
        logger.LogInformation(
            "Paused viewer accrual for stream {StreamSessionId}; live confirmation missing since {MissingSince}",
            message.StreamSessionId,
            message.MissingSince);
        return Task.CompletedTask;
    }
}
