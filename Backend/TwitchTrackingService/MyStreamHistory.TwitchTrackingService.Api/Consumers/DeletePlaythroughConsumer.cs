using MassTransit;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Requests;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Responses;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class DeletePlaythroughConsumer(
    IPlaythroughService playthroughService,
    ILogger<DeletePlaythroughConsumer> logger) : IConsumer<DeletePlaythroughRequestContract>
{
    public async Task Consume(ConsumeContext<DeletePlaythroughRequestContract> context)
    {
        try
        {
            await playthroughService.DeleteAsync(context.Message.TwitchUserId, context.Message.PlaythroughId, context.CancellationToken);
            await context.RespondAsync(new DeletePlaythroughResponseContract { Success = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while deleting playthrough {PlaythroughId}", context.Message.PlaythroughId);
            await context.RespondAsync(new BaseFailedResponseContract { Reason = ex.Message });
        }
    }
}
