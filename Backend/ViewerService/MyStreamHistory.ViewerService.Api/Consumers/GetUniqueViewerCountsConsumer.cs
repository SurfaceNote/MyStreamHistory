using MassTransit;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;
using MyStreamHistory.ViewerService.Application.DTOs;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class GetUniqueViewerCountsConsumer(
    IViewerCategoryWatchRepository repository,
    ILogger<GetUniqueViewerCountsConsumer> logger)
    : IConsumer<GetUniqueViewerCountsRequestContract>
{
    public async Task Consume(ConsumeContext<GetUniqueViewerCountsRequestContract> context)
    {
        try
        {
            var queries = context.Message.Playthroughs.Select(p => new UniqueViewerCountQueryDto
            {
                PlaythroughId = p.PlaythroughId,
                StreamCategoryIds = p.StreamCategoryIds.Distinct().ToArray()
            }).ToArray();

            var counts = await repository.GetUniqueViewerCountsAsync(
                queries,
                context.CancellationToken);

            await context.RespondAsync(new GetUniqueViewerCountsResponseContract
            {
                Counts = counts
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to calculate batch unique viewer counts");
            await context.RespondAsync(new BaseFailedResponseContract
            {
                Reason = $"Error calculating unique viewer counts: {ex.Message}"
            });
        }
    }
}
