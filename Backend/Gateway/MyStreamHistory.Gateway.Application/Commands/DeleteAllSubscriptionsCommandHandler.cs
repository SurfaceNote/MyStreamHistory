using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Requests;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Commands;

/// <summary>
/// Handler for deleting all EventSub subscriptions
/// </summary>
public class DeleteAllSubscriptionsCommandHandler(ITransportBus bus) 
    : IRequestHandler<DeleteAllSubscriptionsCommand, DeleteAllSubscriptionsResponseContract>
{
    public async Task<DeleteAllSubscriptionsResponseContract> Handle(DeleteAllSubscriptionsCommand request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            DeleteAllSubscriptionsRequestContract,
            DeleteAllSubscriptionsResponseContract,
            BaseFailedResponseContract>(
            new DeleteAllSubscriptionsRequestContract(),
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

