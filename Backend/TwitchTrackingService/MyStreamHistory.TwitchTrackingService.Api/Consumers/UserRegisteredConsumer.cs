using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class UserRegisteredConsumer : IConsumer<UserRegisteredEventContract>
{
    private readonly ITwitchApiClient _twitchApiClient;
    private readonly ILogger<UserRegisteredConsumer> _logger;

    public UserRegisteredConsumer(ITwitchApiClient twitchApiClient, ILogger<UserRegisteredConsumer> logger)
    {
        _twitchApiClient = twitchApiClient;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserRegisteredEventContract> context)
    {
        _logger.LogInformation("Received user registered event for {DisplayName} (TwitchId: {TwitchId})", 
            context.Message.DisplayName, context.Message.TwitchUserId);

        try
        {
            // Create stream.online subscription
            await _twitchApiClient.CreateEventSubSubscriptionAsync(
                context.Message.TwitchUserId, "stream.online", context.CancellationToken);

            // Create stream.offline subscription
            await _twitchApiClient.CreateEventSubSubscriptionAsync(
                context.Message.TwitchUserId, "stream.offline", context.CancellationToken);

            _logger.LogInformation("Successfully created EventSub subscriptions for {DisplayName}", context.Message.DisplayName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating EventSub subscriptions for {DisplayName}", context.Message.DisplayName);
            throw;
        }
    }
}

