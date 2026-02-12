using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Requests;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Queries;

/// <summary>
/// Handler for getting chat message EventSub subscriptions
/// </summary>
public class GetChatSubscriptionsQueryHandler(ITransportBus bus) 
    : IRequestHandler<GetChatSubscriptionsQuery, GetChatSubscriptionsResponseContract>
{
    public async Task<GetChatSubscriptionsResponseContract> Handle(GetChatSubscriptionsQuery request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            GetChatSubscriptionsRequestContract,
            GetChatSubscriptionsResponseContract,
            BaseFailedResponseContract>(
            new GetChatSubscriptionsRequestContract(),
            cancellationToken
        );

        if (response.IsSuccess)
        {
            return response.Success!;
        }

        if (response.IsFailure)
        {
            throw new AppException(response.Failure!.Reason);
        }

        throw new InvalidOperationException();
    }
}

