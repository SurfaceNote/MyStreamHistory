using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class ChatMessageConsumer : IConsumer<ChatMessageEventContract>
{
    private readonly IChatMessageBufferService _bufferService;
    private readonly IProcessedEventSubMessageRepository _processedMessageRepository;
    private readonly ILogger<ChatMessageConsumer> _logger;

    public ChatMessageConsumer(
        IChatMessageBufferService bufferService,
        IProcessedEventSubMessageRepository processedMessageRepository,
        ILogger<ChatMessageConsumer> logger)
    {
        _bufferService = bufferService;
        _processedMessageRepository = processedMessageRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ChatMessageEventContract> context)
    {
        var message = context.Message;
        
        // Check for duplicate
        if (await _processedMessageRepository.ExistsAsync(message.MessageId, context.CancellationToken))
        {
            _logger.LogDebug("Duplicate ChatMessage event {MessageId} ignored", message.MessageId);
            return;
        }

        try
        {
            _bufferService.AddChatMessage(
                message.BroadcasterUserId,
                message.ChatterUserId,
                message.CharacterCount);

            // Save processed message
            await _processedMessageRepository.AddAsync(new ProcessedEventSubMessage
            {
                Id = Guid.NewGuid(),
                MessageId = message.MessageId,
                EventType = "channel.chat.message",
                ProcessedAt = DateTime.UtcNow
            }, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ChatMessage event for TwitchUserId: {TwitchUserId}", message.BroadcasterUserId);
            throw;
        }
    }
}

