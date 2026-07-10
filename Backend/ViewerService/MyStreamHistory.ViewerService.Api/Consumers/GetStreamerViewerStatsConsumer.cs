using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.Viewers;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;
using MyStreamHistory.Shared.Base.Error;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class GetStreamerViewerStatsConsumer(
    IViewerStatsRepository viewerStatsRepository,
    IViewerCategoryWatchRepository viewerCategoryWatchRepository,
    ILogger<GetStreamerViewerStatsConsumer> logger)
    : IConsumer<GetStreamerViewerStatsRequestContract>
{
    public async Task Consume(ConsumeContext<GetStreamerViewerStatsRequestContract> context)
    {
        var request = context.Message;

        try
        {
            var stat = await viewerStatsRepository.GetByViewerTwitchIdAndStreamerAsync(
                request.ViewerTwitchUserId,
                request.StreamerTwitchUserId,
                context.CancellationToken);

            if (stat is null)
            {
                await context.RespondAsync(new GetStreamerViewerStatsResponseContract
                {
                    Success = false,
                    Error = ErrorCodes.NotFound
                });
                return;
            }

            var topViewers = await viewerStatsRepository.GetTopViewersByStreamerAsync(
                request.StreamerTwitchUserId,
                100,
                context.CancellationToken);
            var topViewerIndex = topViewers.FindIndex(item => item.ViewerId == stat.ViewerId);

            var categoryWatches = await viewerCategoryWatchRepository.GetByViewerIdAsync(
                stat.ViewerId,
                context.CancellationToken);

            await context.RespondAsync(new GetStreamerViewerStatsResponseContract
            {
                Success = true,
                Stats = new ViewerStatsDto
                {
                    Id = stat.Id,
                    ViewerId = stat.ViewerId,
                    StreamerTwitchUserId = stat.StreamerTwitchUserId,
                    MinutesWatched = stat.MinutesWatched,
                    EarnedMsgPoints = stat.EarnedMsgPoints,
                    Experience = stat.Experience,
                    LastUpdatedAt = stat.LastUpdatedAt,
                    Top100Rank = topViewerIndex >= 0 ? topViewerIndex + 1 : null,
                    Viewer = new ViewerDto
                    {
                        Id = stat.Viewer.Id,
                        TwitchUserId = stat.Viewer.TwitchUserId,
                        DisplayName = stat.Viewer.DisplayName,
                        Login = stat.Viewer.Login,
                        ProfileImageUrl = stat.Viewer.ProfileImageUrl,
                        CreatedAt = stat.Viewer.CreatedAt
                    }
                },
                CategoryWatches = categoryWatches.Select(watch => new ViewerCategoryWatchDto
                {
                    Id = watch.Id,
                    ViewerId = watch.ViewerId,
                    StreamCategoryId = watch.StreamCategoryId,
                    MinutesWatched = watch.MinutesWatched,
                    ChatPoints = watch.ChatPoints,
                    Experience = watch.Experience,
                    LastUpdatedAt = watch.LastUpdatedAt
                }).ToList()
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to get viewer {ViewerTwitchUserId} statistics for streamer {StreamerTwitchUserId}",
                request.ViewerTwitchUserId,
                request.StreamerTwitchUserId);

            await context.RespondAsync(new GetStreamerViewerStatsResponseContract
            {
                Success = false,
                Error = exception.Message
            });
        }
    }
}
