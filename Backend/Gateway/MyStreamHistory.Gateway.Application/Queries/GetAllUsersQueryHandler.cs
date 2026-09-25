using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Users;
using MyStreamHistory.Shared.Base.Contracts.Users.Requests;
using MyStreamHistory.Shared.Base.Contracts.Users.Response;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetAllUsersQueryHandler(ITransportBus bus) : IRequestHandler<GetAllUsersQuery, List<UserDto>>
{
    public async Task<List<UserDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            GetAllUsersRequestContract, GetAllUsersResponseContract, BaseFailedResponseContract>
        (
            new GetAllUsersRequestContract(), cancellationToken
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
