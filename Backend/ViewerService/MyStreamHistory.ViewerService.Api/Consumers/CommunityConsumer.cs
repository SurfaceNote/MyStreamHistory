using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.Community;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class CommunityConsumer(ICommunityService service) : IConsumer<CommunityRequest>
{
    public async Task Consume(ConsumeContext<CommunityRequest> context) =>
        await context.RespondAsync(await service.HandleAsync(context.Message, context.CancellationToken));
}
