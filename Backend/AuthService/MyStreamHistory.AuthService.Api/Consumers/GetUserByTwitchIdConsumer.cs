using MassTransit;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Users;
using MyStreamHistory.Shared.Base.Contracts.Users.Requests;
using MyStreamHistory.Shared.Base.Contracts.Users.Response;

namespace MyStreamHistory.AuthService.Api.Consumers;

public class GetUserByTwitchIdConsumer : IConsumer<GetUserByTwitchIdRequestContract>
{
    private readonly IAuthUserRepository _authUserRepository;
    private readonly ILogger<GetUserByTwitchIdConsumer> _logger;

    public GetUserByTwitchIdConsumer(IAuthUserRepository authUserRepository, ILogger<GetUserByTwitchIdConsumer> logger)
    {
        _authUserRepository = authUserRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetUserByTwitchIdRequestContract> context)
    {
        _logger.LogInformation("Received request to get user by TwitchId {TwitchId}", context.Message.TwitchId);

        try
        {
            var user = await _authUserRepository.GetUserByTwitchIdAsync(context.Message.TwitchId);

            if (user == null)
            {
                _logger.LogWarning("User with TwitchId {TwitchId} not found", context.Message.TwitchId);
                await context.RespondAsync(new BaseFailedResponseContract
                {
                    Reason = $"User with TwitchId {context.Message.TwitchId} not found"
                });
                return;
            }

            var response = new GetUserByTwitchIdResponseContract
            {
                User = new UserDto
                {
                    TwitchId = user.TwitchId,
                    DisplayName = user.DisplayName,
                    Avatar = user.Avatar
                }
            };

            await context.RespondAsync(response);
            _logger.LogInformation("Successfully responded with user data for TwitchId {TwitchId}", context.Message.TwitchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting user by TwitchId {TwitchId}", context.Message.TwitchId);
            
            await context.RespondAsync(new BaseFailedResponseContract
            {
                Reason = $"Error getting user: {ex.Message}"
            });
        }
    }
}

