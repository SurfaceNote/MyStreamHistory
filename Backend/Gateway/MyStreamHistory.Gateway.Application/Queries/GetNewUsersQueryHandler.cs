using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Users;
using MyStreamHistory.Shared.Base.Contracts.Users.Requests;
using MyStreamHistory.Shared.Base.Contracts.Users.Response;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetNewUsersQueryHandler(ITransportBus bus) : IRequestHandler<GetNewUsersQuery, List<UserDto>>
{
    public async Task<List<UserDto>> Handle(GetNewUsersQuery request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            GetNewUsersRequestContract, GetNewUsersResponseContract, BaseFailedResponseContract>
        (
            new GetNewUsersRequestContract(), cancellationToken
        );

        if (response.IsSuccess)
        {
            return response.Success!.Users.Select(u => new UserDto
            {
                TwitchId = u.TwitchId,
                DisplayName = u.DisplayName,
                Avatar = u.Avatar
            }).ToList();
        }

        if (response.IsFailure)
        {
            throw new AppException(response.Failure!.Reason);
        }

        throw new InvalidOperationException();
    }
}
