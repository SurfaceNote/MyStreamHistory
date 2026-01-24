using MassTransit;
using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Requests;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;
using MyStreamHistory.Shared.Base.Contracts.Users.Requests;
using MyStreamHistory.Shared.Base.Contracts.Users.Response;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

/// <summary>
/// Consumer for subscribing to all registered users
/// </summary>
public class SubscribeToAllUsersConsumer : IConsumer<SubscribeToAllUsersRequestContract>
{
    private readonly ITwitchApiClient _twitchApiClient;
    private readonly ITransportBus _transportBus;
    private readonly ILogger<SubscribeToAllUsersConsumer> _logger;

    public SubscribeToAllUsersConsumer(
        ITwitchApiClient twitchApiClient,
        ITransportBus transportBus,
        ILogger<SubscribeToAllUsersConsumer> logger)
    {
        _twitchApiClient = twitchApiClient;
        _transportBus = transportBus;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SubscribeToAllUsersRequestContract> context)
    {
        _logger.LogInformation("Received request to subscribe to all users");

        try
        {
            // Get all users from AuthService
            var usersResponse = await _transportBus.SendRequestAsync<GetAllUsersRequestContract, GetAllUsersResponseContract>(
                new GetAllUsersRequestContract(),
                context.CancellationToken);

            if (usersResponse.Success == null || usersResponse.Success.Users == null || !usersResponse.Success.Users.Any())
            {
                _logger.LogWarning("No users found in AuthService");
                
                var emptyResponse = new SubscribeToAllUsersResponseContract
                {
                    UserCount = 0,
                    SuccessCount = 0,
                    FailCount = 0,
                    Message = "No users found to subscribe to"
                };

                await context.RespondAsync(emptyResponse);
                return;
            }

            var allUsers = usersResponse.Success.Users;
            var userIds = allUsers.Select(u => u.TwitchId).ToList();
            
            _logger.LogInformation("Found {UserCount} users to subscribe to", userIds.Count);

            var (successCount, failCount) = await _twitchApiClient.SubscribeToAllUsersAsync(userIds, context.CancellationToken);

            var response = new SubscribeToAllUsersResponseContract
            {
                UserCount = userIds.Count,
                SuccessCount = successCount,
                FailCount = failCount,
                Message = $"Subscribed to {userIds.Count} users. Success: {successCount}, Failed: {failCount}"
            };

            _logger.LogInformation("Subscription complete. Users: {UserCount}, Success: {SuccessCount}, Failed: {FailCount}", 
                userIds.Count, successCount, failCount);

            await context.RespondAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subscribing to all users");
            throw;
        }
    }
}

