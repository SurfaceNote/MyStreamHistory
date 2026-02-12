using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class StreamEndedConsumer : IConsumer<StreamEndedEventContract>
{
    private readonly IViewerTrackingService _trackingService;
    private readonly ILogger<StreamEndedConsumer> _logger;

    public StreamEndedConsumer(
        IViewerTrackingService trackingService,
        ILogger<StreamEndedConsumer> logger)
    {
        _trackingService = trackingService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<StreamEndedEventContract> context)
    {
        var message = context.Message;

        _logger.LogInformation("Processing StreamEnded event for TwitchUserId: {TwitchUserId}, StreamSessionId: {StreamSessionId}", 
            message.BroadcasterUserId, message.StreamSessionId);

        try
        {
            await _trackingService.HandleStreamOfflineAsync(
                message.BroadcasterUserId.ToString(),
                context.CancellationToken);

            _logger.LogInformation("Successfully processed StreamEnded event for TwitchUserId: {TwitchUserId}", message.BroadcasterUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing StreamEnded event for TwitchUserId: {TwitchUserId}", message.BroadcasterUserId);
            throw;
        }
    }
}

