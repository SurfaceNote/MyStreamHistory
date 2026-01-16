using MassTransit;
using Microsoft.EntityFrameworkCore;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.Shared.Base.Contracts.Users;
using MyStreamHistory.Shared.Base.Contracts.Users.Requests;
using MyStreamHistory.Shared.Base.Contracts.Users.Response;

namespace MyStreamHistory.AuthService.Api.Consumers;

public class GetNewUsersConsumer(IAuthUserRepository authUserRepository) : IConsumer<GetNewUsersRequestContract>
{
    public async Task Consume(ConsumeContext<GetNewUsersRequestContract> context)
    {
        var newUsers = await authUserRepository.Query()
            .OrderByDescending(u => u.SiteCreatedAt)
            .Take(10)
            .ToListAsync();

        var response = new GetNewUsersResponseContract
        {
            Users = newUsers.Select(u => new UserDto
            {
                TwitchId = u.TwitchId,
                DisplayName = u.DisplayName,
                Avatar = u.Avatar
            }).ToList()
        };
        
        await context.RespondAsync<GetNewUsersResponseContract>(response);
    }
}