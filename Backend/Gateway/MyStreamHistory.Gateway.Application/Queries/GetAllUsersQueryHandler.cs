using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;
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
            var users = response.Success!.Users;
            if (users.Count == 0)
            {
                return new List<UserDto>();
            }

            var liveResponse = await bus.SendRequestAsync<
                GetLiveStreamersRequestContract, GetLiveStreamersResponseContract, BaseFailedResponseContract>
            (
                new GetLiveStreamersRequestContract(), cancellationToken
            );

            if (liveResponse.IsFailure)
            {
                throw new AppException(liveResponse.Failure!.Reason);
            }

            if (!liveResponse.IsSuccess)
            {
                throw new InvalidOperationException();
            }

            var liveIds = liveResponse.Success!.TwitchUserIds.ToHashSet();
            return users.Select(u => new UserDto
            {
                TwitchId = u.TwitchId,
                DisplayName = u.DisplayName,
                Avatar = u.Avatar,
                IsLive = liveIds.Contains(u.TwitchId)
            }).ToList();
        }

        if (response.IsFailure)
        {
            throw new AppException(response.Failure!.Reason);
        }

        throw new InvalidOperationException();
    }
}
