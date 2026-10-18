using MyStreamHistory.Shared.Api.Features;
using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class StreamCategoryChangedConsumer : IConsumer<StreamCategoryChangedEventContract>
{
    private readonly TwitchDataCollectionFeature _dataCollection;
    private readonly IChatMessageBufferService _bufferService;
    private readonly IProcessedEventSubMessageRepository _processedMessageRepository;
    private readonly ILogger<StreamCategoryChangedConsumer> _logger;
    private readonly IStreamCategoryService _categoryService;
    private readonly IViewerTrackingService _trackingService;

    public StreamCategoryChangedConsumer(
        TwitchDataCollectionFeature dataCollection,
        IChatMessageBufferService bufferService,
        IProcessedEventSubMessageRepository processedMessageRepository,
        ILogger<StreamCategoryChangedConsumer> logger,
        IStreamCategoryService categoryService,
        IViewerTrackingService trackingService)
    {
        _dataCollection = dataCollection;
        _bufferService = bufferService;
        _processedMessageRepository = processedMessageRepository;
        _logger = logger;
        _categoryService = categoryService;
        _trackingService = trackingService;
    }

    public async Task Consume(ConsumeContext<StreamCategoryChangedEventContract> context)
    {
        if (!_dataCollection.Enabled) return;

        var message = context.Message;
        
        // Check for duplicate
        if (await _processedMessageRepository.ExistsAsync(message.MessageId, context.CancellationToken))
        {
            _logger.LogInformation("Duplicate StreamCategoryChanged event {MessageId} ignored", message.MessageId);
            return;
        }

        _logger.LogInformation("Processing StreamCategoryChanged event for StreamSessionId: {StreamSessionId}, TwitchUserId: {TwitchUserId}, Category: {CategoryName}", 
            message.StreamSessionId, message.BroadcasterUserId, message.CategoryName);

        try
        {
            // Update buffer with new category
            var updated = _bufferService.UpdateStreamCategory(
                message.BroadcasterUserId.ToString(),
                message.StreamSessionId,
                message.NewCategoryId);

            if (!updated)
            {
                // Category and stream-created events use separate queues and can arrive in either order.
                // Verify the session before initializing so an old event cannot replace a newer stream.
                var twitchUserId = message.BroadcasterUserId.ToString();
                var activeStream = await _categoryService.GetActiveStreamCategoryAsync(twitchUserId, context.CancellationToken);
                if (activeStream?.StreamSessionId == message.StreamSessionId && activeStream.IsLiveConfirmed)
                {
                    await _trackingService.HandleStreamOnlineAsync(
                        twitchUserId,
                        message.StreamSessionId,
                        activeStream.StreamCategoryId ?? message.NewCategoryId,
                        context.CancellationToken);
                    _logger.LogInformation(
                        "Recovered stream buffer from category event for TwitchUserId {TwitchUserId}, StreamSessionId {StreamSessionId}",
                        twitchUserId, message.StreamSessionId);
                }
                else
                {
                    _logger.LogWarning(
                        "Ignored category event without a matching confirmed active stream for TwitchUserId {TwitchUserId}, StreamSessionId {StreamSessionId}",
                        twitchUserId, message.StreamSessionId);
                }
            }

            // Save processed message
            await _processedMessageRepository.AddAsync(new ProcessedEventSubMessage
            {
                Id = Guid.NewGuid(),
                MessageId = message.MessageId,
                EventType = "stream.category.changed",
                ProcessedAt = DateTime.UtcNow
            }, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing StreamCategoryChanged event for StreamSessionId: {StreamSessionId}", message.StreamSessionId);
            throw;
        }
    }
}

