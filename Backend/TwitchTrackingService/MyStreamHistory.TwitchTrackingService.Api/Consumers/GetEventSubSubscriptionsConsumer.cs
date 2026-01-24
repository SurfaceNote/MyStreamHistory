using MassTransit;
using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Requests;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

/// <summary>
/// Consumer for getting EventSub subscriptions
/// </summary>
public class GetEventSubSubscriptionsConsumer : IConsumer<GetEventSubSubscriptionsRequestContract>
{
    private readonly ITwitchApiClient _twitchApiClient;
    private readonly ILogger<GetEventSubSubscriptionsConsumer> _logger;

    public GetEventSubSubscriptionsConsumer(
        ITwitchApiClient twitchApiClient,
        ILogger<GetEventSubSubscriptionsConsumer> logger)
    {
        _twitchApiClient = twitchApiClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetEventSubSubscriptionsRequestContract> context)
    {
        _logger.LogInformation("Received request to get EventSub subscriptions");

        try
        {
            var subscriptions = await _twitchApiClient.GetEventSubSubscriptionsAsync(context.CancellationToken);

            var response = new GetEventSubSubscriptionsResponseContract
            {
                Subscriptions = new EventSubSubscriptionsDto
                {
                    Data = subscriptions.Data.Select(s => new EventSubSubscriptionDto
                    {
                        Id = s.Id,
                        Type = s.Type,
                        Version = s.Version,
                        Status = s.Status,
                        Cost = s.Cost,
                        Condition = new EventSubConditionDto
                        {
                            BroadcasterUserId = s.Condition.BroadcasterUserId
                        },
                        Transport = new EventSubTransportDto
                        {
                            Method = s.Transport.Method,
                            Callback = s.Transport.Callback
                        },
                        CreatedAt = s.CreatedAt
                    }).ToList(),
                    Total = subscriptions.Total,
                    TotalCost = subscriptions.TotalCost,
                    MaxTotalCost = subscriptions.MaxTotalCost
                }
            };

            _logger.LogInformation("Returning {Count} EventSub subscriptions. Cost: {Cost}/{MaxCost}",
                response.Subscriptions.Total,
                response.Subscriptions.TotalCost,
                response.Subscriptions.MaxTotalCost);

            await context.RespondAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting EventSub subscriptions");
            throw;
        }
    }
}

