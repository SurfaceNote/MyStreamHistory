using MassTransit;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Viewers;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class GetStreamViewersConsumer : IConsumer<GetStreamViewersRequestContract>
{
    private readonly IViewerCategoryWatchRepository _categoryWatchRepository;
    private readonly ILogger<GetStreamViewersConsumer> _logger;

    public GetStreamViewersConsumer(
        IViewerCategoryWatchRepository categoryWatchRepository,
        ILogger<GetStreamViewersConsumer> logger)
    {
        _categoryWatchRepository = categoryWatchRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetStreamViewersRequestContract> context)
    {
        var request = context.Message;

        _logger.LogInformation("Processing GetStreamViewers request for StreamSessionId: {StreamSessionId}", 
            request.StreamSessionId);

        try
        {
            // First, we need to get all category IDs for this stream session
            // This is done through StreamCategories in TwitchTrackingService
            // However, we receive StreamCategoryIds in the request
            
            // For now, we'll get viewers by the categories associated with the stream
            // The Gateway will need to first get the stream details with categories,
            // then request viewers for those categories
            
            var viewers = new List<ViewerCategoryWatchDto>();

            if (request.StreamCategoryIds != null && request.StreamCategoryIds.Any())
            {
                var watches = await _categoryWatchRepository.GetByStreamCategoryIdsAsync(
                    request.StreamCategoryIds, 
                    context.CancellationToken);

                // Group by viewer and aggregate stats
                var viewerGroups = watches
                    .GroupBy(w => w.ViewerId)
                    .Select(g => new
                    {
                        Viewer = g.First().Viewer,
                        TotalMinutesWatched = g.Sum(w => w.MinutesWatched),
                        TotalChatPoints = g.Sum(w => w.ChatPoints),
                        TotalExperience = g.Sum(w => w.Experience)
                    })
                    .OrderByDescending(v => v.TotalExperience)
                    .ToList();

                viewers = viewerGroups.Select(v => new ViewerCategoryWatchDto
                {
                    Id = Guid.NewGuid(),
                    ViewerId = v.Viewer.Id,
                    StreamCategoryId = Guid.Empty, // Aggregated across all categories
                    MinutesWatched = v.TotalMinutesWatched,
                    ChatPoints = v.TotalChatPoints,
                    Experience = v.TotalExperience,
                    LastUpdatedAt = DateTime.UtcNow,
                    Viewer = new ViewerDto
                    {
                        Id = v.Viewer.Id,
                        TwitchUserId = v.Viewer.TwitchUserId,
                        DisplayName = v.Viewer.DisplayName,
                        Login = v.Viewer.Login,
                        ProfileImageUrl = v.Viewer.ProfileImageUrl,
                        CreatedAt = v.Viewer.CreatedAt
                    }
                }).ToList();
            }

            await context.RespondAsync(new GetStreamViewersResponseContract
            {
                Success = true,
                Viewers = viewers
            });

            _logger.LogInformation("Successfully processed GetStreamViewers request for StreamSessionId: {StreamSessionId}, found {Count} viewers",
                request.StreamSessionId, viewers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing GetStreamViewers request for StreamSessionId: {StreamSessionId}",
                request.StreamSessionId);

            await context.RespondAsync(new GetStreamViewersResponseContract
            {
                Success = false,
                Error = ex.Message,
                Viewers = new List<ViewerCategoryWatchDto>()
            });
        }
    }
}

