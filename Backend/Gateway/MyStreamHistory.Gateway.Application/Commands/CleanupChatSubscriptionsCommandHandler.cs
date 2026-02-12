using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Requests;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Commands;

/// <summary>
/// Handler for cleaning up all chat message EventSub subscriptions
/// </summary>
public class CleanupChatSubscriptionsCommandHandler(ITransportBus bus) 
    : IRequestHandler<CleanupChatSubscriptionsCommand, CleanupChatSubscriptionsResponseContract>
{
    public async Task<CleanupChatSubscriptionsResponseContract> Handle(CleanupChatSubscriptionsCommand request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            CleanupChatSubscriptionsRequestContract,
            CleanupChatSubscriptionsResponseContract,
            BaseFailedResponseContract>(
            new CleanupChatSubscriptionsRequestContract(),
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

