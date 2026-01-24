using MassTransit;
using Microsoft.EntityFrameworkCore;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.Shared.Base.Contracts.Users;
using MyStreamHistory.Shared.Base.Contracts.Users.Requests;
using MyStreamHistory.Shared.Base.Contracts.Users.Response;

namespace MyStreamHistory.AuthService.Api.Consumers;

public class GetAllUsersConsumer(IAuthUserRepository authUserRepository) : IConsumer<GetAllUsersRequestContract>
{
    public async Task Consume(ConsumeContext<GetAllUsersRequestContract> context)
    {
        var users = await authUserRepository.Query()
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

        var response = new GetAllUsersResponseContract
        {
            Users = users.Select(u => new UserDto
            {
                TwitchId = u.TwitchId,
                DisplayName = u.DisplayName,
                Avatar = u.Avatar
            }).ToList()
        };

        await context.RespondAsync(response);
    }
}