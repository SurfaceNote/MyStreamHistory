using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Requests;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class GetChatSubscriptionsConsumer : IConsumer<GetChatSubscriptionsRequestContract>
{
    private readonly ITwitchEventSubClient _eventSubClient;
    private readonly ILogger<GetChatSubscriptionsConsumer> _logger;

    public GetChatSubscriptionsConsumer(
        ITwitchEventSubClient eventSubClient,
        ILogger<GetChatSubscriptionsConsumer> logger)
    {
        _eventSubClient = eventSubClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetChatSubscriptionsRequestContract> context)
    {
        _logger.LogInformation("Processing GetChatSubscriptions request");

        try
        {
            var subscriptions = await _eventSubClient.GetSubscriptionsAsync("channel.chat.message", context.CancellationToken);

            var response = new GetChatSubscriptionsResponseContract
            {
                Count = subscriptions.Count,
                Subscriptions = subscriptions.Select(s => new ChatSubscriptionDto
                {
                    Id = s.Id,
                    Status = s.Status,
                    BroadcasterUserId = s.Condition?.BroadcasterUserId ?? string.Empty,
                    CreatedAt = s.CreatedAt
                }).ToList()
            };

            _logger.LogInformation("Found {Count} chat message subscriptions", response.Count);

            await context.RespondAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting chat subscriptions");
            throw;
        }
    }
}

