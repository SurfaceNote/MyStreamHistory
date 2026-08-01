using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class StreamCategoryChangedConsumer : IConsumer<StreamCategoryChangedEventContract>
{
    private readonly IChatMessageBufferService _bufferService;
    private readonly IProcessedEventSubMessageRepository _processedMessageRepository;
    private readonly ILogger<StreamCategoryChangedConsumer> _logger;

    public StreamCategoryChangedConsumer(
        IChatMessageBufferService bufferService,
        IProcessedEventSubMessageRepository processedMessageRepository,
        ILogger<StreamCategoryChangedConsumer> logger)
    {
        _bufferService = bufferService;
        _processedMessageRepository = processedMessageRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<StreamCategoryChangedEventContract> context)
    {
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
                _logger.LogWarning(
                    "Ignored stale category event for TwitchUserId {TwitchUserId}, StreamSessionId {StreamSessionId}",
                    message.BroadcasterUserId,
                    message.StreamSessionId);
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

