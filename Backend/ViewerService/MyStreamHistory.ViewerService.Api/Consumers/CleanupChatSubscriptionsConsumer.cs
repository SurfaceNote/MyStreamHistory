using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Requests;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class CleanupChatSubscriptionsConsumer : IConsumer<CleanupChatSubscriptionsRequestContract>
{
    private readonly ITwitchEventSubClient _eventSubClient;
    private readonly ILogger<CleanupChatSubscriptionsConsumer> _logger;

    public CleanupChatSubscriptionsConsumer(
        ITwitchEventSubClient eventSubClient,
        ILogger<CleanupChatSubscriptionsConsumer> logger)
    {
        _eventSubClient = eventSubClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CleanupChatSubscriptionsRequestContract> context)
    {
        _logger.LogWarning("Processing CleanupChatSubscriptions request - this will delete ALL chat subscriptions");

        try
        {
            var deletedCount = await _eventSubClient.CleanupAllChatSubscriptionsAsync(context.CancellationToken);

            var response = new CleanupChatSubscriptionsResponseContract
            {
                DeletedCount = deletedCount,
                Message = $"Successfully cleaned up {deletedCount} chat message subscriptions"
            };

            _logger.LogWarning("Cleaned up {DeletedCount} chat message subscriptions", deletedCount);

            await context.RespondAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while cleaning up chat subscriptions");
            throw;
        }
    }
}

