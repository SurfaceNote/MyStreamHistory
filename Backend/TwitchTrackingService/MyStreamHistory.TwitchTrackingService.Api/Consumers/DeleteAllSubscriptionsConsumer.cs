using MyStreamHistory.Shared.Api.Features;
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
    private readonly TwitchDataCollectionFeature _dataCollection;
    private readonly ITwitchApiClient _twitchApiClient;
    private readonly ILogger<DeleteAllSubscriptionsConsumer> _logger;

    public DeleteAllSubscriptionsConsumer(
        TwitchDataCollectionFeature dataCollection,
        ITwitchApiClient twitchApiClient,
        ILogger<DeleteAllSubscriptionsConsumer> logger)
    {
        _dataCollection = dataCollection;
        _twitchApiClient = twitchApiClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<DeleteAllSubscriptionsRequestContract> context)
    {
        _dataCollection.EnsureEnabled();

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

