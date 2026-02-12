using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class StreamCreatedConsumer : IConsumer<StreamCreatedEventContract>
{
    private readonly IViewerTrackingService _trackingService;
    private readonly IStreamCategoryService _categoryService;
    private readonly ILogger<StreamCreatedConsumer> _logger;

    public StreamCreatedConsumer(
        IViewerTrackingService trackingService,
        IStreamCategoryService categoryService,
        ILogger<StreamCreatedConsumer> logger)
    {
        _trackingService = trackingService;
        _categoryService = categoryService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<StreamCreatedEventContract> context)
    {
        var message = context.Message;

        _logger.LogInformation("Processing StreamCreated event for TwitchUserId: {TwitchUserId}, StreamSessionId: {StreamSessionId}", 
            message.BroadcasterUserId, message.StreamSessionId);

        try
        {
            // Get current category from TwitchTrackingService
            var categoryInfo = await _categoryService.GetActiveStreamCategoryAsync(
                message.BroadcasterUserId.ToString(), 
                context.CancellationToken);

            var currentCategoryId = categoryInfo?.StreamCategoryId;

            // Handle stream online - use StreamSessionId from event
            await _trackingService.HandleStreamOnlineAsync(
                message.BroadcasterUserId.ToString(),
                message.StreamSessionId,
                currentCategoryId,
                context.CancellationToken);

            _logger.LogInformation("Successfully processed StreamCreated event for TwitchUserId: {TwitchUserId}", message.BroadcasterUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing StreamCreated event for TwitchUserId: {TwitchUserId}", message.BroadcasterUserId);
            throw;
        }
    }
}

