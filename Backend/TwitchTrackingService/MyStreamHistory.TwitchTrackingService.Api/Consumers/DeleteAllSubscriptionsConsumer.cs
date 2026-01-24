using MassTransit;
using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Requests;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

/// <summary>
/// Consumer for deleting all EventSub subscriptions
/// </summary>
public class DeleteAllSubscriptionsConsumer : IConsumer<DeleteAllSubscriptionsRequestContract>
{
    private readonly ITwitchApiClient _twitchApiClient;
    private readonly ILogger<DeleteAllSubscriptionsConsumer> _logger;

    public DeleteAllSubscriptionsConsumer(
        ITwitchApiClient twitchApiClient,
        ILogger<DeleteAllSubscriptionsConsumer> logger)
    {
        _twitchApiClient = twitchApiClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DeleteAllSubscriptionsRequestContract> context)
    {
        _logger.LogWarning("Received request to delete ALL EventSub subscriptions");

        try
        {
            var deletedCount = await _twitchApiClient.DeleteAllSubscriptionsAsync(context.CancellationToken);

            var response = new DeleteAllSubscriptionsResponseContract
            {
                DeletedCount = deletedCount,
                Message = $"Successfully deleted {deletedCount} subscriptions"
            };

            _logger.LogInformation("Deleted {DeletedCount} EventSub subscriptions", deletedCount);

            await context.RespondAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting all EventSub subscriptions");
            throw;
        }
    }
}

