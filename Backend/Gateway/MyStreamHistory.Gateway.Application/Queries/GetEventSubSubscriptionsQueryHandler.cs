using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Requests;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Queries;

/// <summary>
/// Handler for getting EventSub subscriptions
/// </summary>
public class GetEventSubSubscriptionsQueryHandler(ITransportBus bus) 
    : IRequestHandler<GetEventSubSubscriptionsQuery, EventSubSubscriptionsDto>
{
    public async Task<EventSubSubscriptionsDto> Handle(GetEventSubSubscriptionsQuery request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            GetEventSubSubscriptionsRequestContract,
            GetEventSubSubscriptionsResponseContract,
            BaseFailedResponseContract>(
            new GetEventSubSubscriptionsRequestContract(),
            cancellationToken
        );

        if (response.IsSuccess)
        {
            return response.Success!.Subscriptions;
        }

        if (response.IsFailure)
        {
            throw new AppException(response.Failure!.Reason);
        }

        throw new InvalidOperationException();
    }
}

