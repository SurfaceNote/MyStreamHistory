using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class ChannelUpdateConsumer : IConsumer<ChannelUpdateEventContract>
{
    private readonly ICategoryTrackingService _categoryTrackingService;
    private readonly IStreamSessionRepository _streamSessionRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ChannelUpdateConsumer> _logger;

    public ChannelUpdateConsumer(
        ICategoryTrackingService categoryTrackingService,
        IStreamSessionRepository streamSessionRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<ChannelUpdateConsumer> logger)
    {
        _categoryTrackingService = categoryTrackingService;
        _streamSessionRepository = streamSessionRepository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ChannelUpdateEventContract> context)
    {
        var message = context.Message;
        
        _logger.LogInformation("Received channel.update event for {BroadcasterUserLogin}, category: {CategoryName}", 
            message.BroadcasterUserLogin, message.CategoryName);

        try
        {
            // Find active stream session
            var allSessions = await _streamSessionRepository.GetAllAsync(context.CancellationToken);
            var activeSession = allSessions.FirstOrDefault(s => 
                s.TwitchUserId == message.BroadcasterUserId && s.IsLive);

            if (activeSession == null)
            {
                _logger.LogWarning("No active stream session found for broadcaster {BroadcasterUserLogin}", 
                    message.BroadcasterUserLogin);
                return;
            }

            // Process category change
            var categoryChanged = await _categoryTrackingService.ProcessSingleStreamCategoryAsync(
                activeSession.Id, 
                message.CategoryId, 
                message.CategoryName,
                context.CancellationToken);

            if (categoryChanged)
            {
                _logger.LogInformation("Category changed processed for stream {StreamSessionId}", activeSession.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing channel.update event for {BroadcasterUserLogin}", 
                message.BroadcasterUserLogin);
            throw;
        }
    }
}

