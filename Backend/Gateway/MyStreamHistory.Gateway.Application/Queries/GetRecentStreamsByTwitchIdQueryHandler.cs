using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetRecentStreamsByTwitchIdQueryHandler(ITransportBus bus) 
    : IRequestHandler<GetRecentStreamsByTwitchIdQuery, List<StreamSessionDto>>
{
    public async Task<List<StreamSessionDto>> Handle(GetRecentStreamsByTwitchIdQuery request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            GetRecentStreamsRequestContract, GetRecentStreamsResponseContract, BaseFailedResponseContract>
        (
            new GetRecentStreamsRequestContract 
            { 
                TwitchUserId = request.TwitchUserId, 
                Count = request.Count 
            }, 
            cancellationToken
        );

        if (response.IsSuccess)
        {
            return response.Success!.StreamSessions;
        }

        if (response.IsFailure)
        {
            throw new AppException(response.Failure!.Reason);
        }

        throw new InvalidOperationException();
    }
}

