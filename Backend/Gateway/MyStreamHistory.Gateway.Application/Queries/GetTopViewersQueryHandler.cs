using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Viewers;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetTopViewersQueryHandler(ITransportBus bus) : IRequestHandler<GetTopViewersQuery, List<ViewerStatsDto>>
{
    public async Task<List<ViewerStatsDto>> Handle(GetTopViewersQuery request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            GetTopViewersRequestContract, 
            GetTopViewersResponseContract,
            BaseFailedResponseContract>
        (
            new GetTopViewersRequestContract 
            { 
                StreamerTwitchUserId = request.StreamerTwitchUserId,
                Limit = request.Limit
            }, 
            cancellationToken
        );

        if (response.IsFailure)
        {
            throw new AppException(response.Failure!.Reason);
        }

        if (!response.Success!.Success)
        {
            throw new AppException(response.Success.Error ?? "Failed to get top viewers");
        }

        return response.Success.TopViewers;
    }
}

