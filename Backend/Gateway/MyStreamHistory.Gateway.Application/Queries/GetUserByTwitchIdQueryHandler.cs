using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Users;
using MyStreamHistory.Shared.Base.Contracts.Users.Requests;
using MyStreamHistory.Shared.Base.Contracts.Users.Response;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetUserByTwitchIdQueryHandler(ITransportBus bus) : IRequestHandler<GetUserByTwitchIdQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserByTwitchIdQuery request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            GetUserByTwitchIdRequestContract, GetUserByTwitchIdResponseContract, BaseFailedResponseContract>
        (
            new GetUserByTwitchIdRequestContract { TwitchId = request.TwitchId }, cancellationToken
        );

        if (response.IsSuccess)
        {
            var user = response.Success!.User;
            return new UserDto
            {
                TwitchId = user.TwitchId,
                DisplayName = user.DisplayName,
                Avatar = user.Avatar
            };
        }

        if (response.IsFailure)
        {
            throw new AppException(response.Failure!.Reason);
        }

        throw new InvalidOperationException();
    }
}

