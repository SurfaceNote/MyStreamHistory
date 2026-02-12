using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.Viewers;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class GetTopViewersConsumer : IConsumer<GetTopViewersRequestContract>
{
    private readonly IViewerStatsRepository _viewerStatsRepository;
    private readonly ILogger<GetTopViewersConsumer> _logger;

    public GetTopViewersConsumer(
        IViewerStatsRepository viewerStatsRepository,
        ILogger<GetTopViewersConsumer> logger)
    {
        _viewerStatsRepository = viewerStatsRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetTopViewersRequestContract> context)
    {
        var request = context.Message;

        _logger.LogInformation("Processing GetTopViewers request for StreamerTwitchUserId: {StreamerTwitchUserId}, Limit: {Limit}", 
            request.StreamerTwitchUserId, request.Limit);

        try
        {
            var topViewers = await _viewerStatsRepository.GetTopViewersByStreamerAsync(
                request.StreamerTwitchUserId, 
                request.Limit,
                context.CancellationToken);

            var viewerStatsDtos = topViewers.Select(stat => new ViewerStatsDto
            {
                Id = stat.Id,
                ViewerId = stat.ViewerId,
                StreamerTwitchUserId = stat.StreamerTwitchUserId,
                MinutesWatched = stat.MinutesWatched,
                EarnedMsgPoints = stat.EarnedMsgPoints,
                Experience = stat.Experience,
                LastUpdatedAt = stat.LastUpdatedAt,
                Viewer = new ViewerDto
                {
                    Id = stat.Viewer.Id,
                    TwitchUserId = stat.Viewer.TwitchUserId,
                    DisplayName = stat.Viewer.DisplayName,
                    Login = stat.Viewer.Login,
                    ProfileImageUrl = stat.Viewer.ProfileImageUrl,
                    CreatedAt = stat.Viewer.CreatedAt
                }
            }).ToList();

            await context.RespondAsync(new GetTopViewersResponseContract
            {
                Success = true,
                TopViewers = viewerStatsDtos
            });

            _logger.LogInformation("Successfully processed GetTopViewers request for StreamerTwitchUserId: {StreamerTwitchUserId}, found {Count} viewers",
                request.StreamerTwitchUserId, viewerStatsDtos.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing GetTopViewers request for StreamerTwitchUserId: {StreamerTwitchUserId}",
                request.StreamerTwitchUserId);

            await context.RespondAsync(new GetTopViewersResponseContract
            {
                Success = false,
                Error = ex.Message,
                TopViewers = new List<ViewerStatsDto>()
            });
        }
    }
}

