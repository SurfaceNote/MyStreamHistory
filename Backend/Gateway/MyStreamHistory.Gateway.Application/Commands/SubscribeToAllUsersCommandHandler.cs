using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Requests;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Commands;

/// <summary>
/// Handler for subscribing to all registered users
/// </summary>
public class SubscribeToAllUsersCommandHandler(ITransportBus bus) 
    : IRequestHandler<SubscribeToAllUsersCommand, SubscribeToAllUsersResponseContract>
{
    public async Task<SubscribeToAllUsersResponseContract> Handle(SubscribeToAllUsersCommand request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            SubscribeToAllUsersRequestContract,
            SubscribeToAllUsersResponseContract,
            BaseFailedResponseContract>(
            new SubscribeToAllUsersRequestContract(),
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

