using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Requests;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Responses;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Commands;

public class DeletePlaythroughCommandHandler(ITransportBus bus) : IRequestHandler<DeletePlaythroughCommand>
{
    public async Task Handle(DeletePlaythroughCommand request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            DeletePlaythroughRequestContract,
            DeletePlaythroughResponseContract,
            BaseFailedResponseContract>(
            new DeletePlaythroughRequestContract
            {
                TwitchUserId = request.TwitchUserId,
                PlaythroughId = request.PlaythroughId
            },
            cancellationToken);

        if (response.IsSuccess)
        {
            return;
        }

        if (response.IsFailure)
        {
            throw new AppException(response.Failure!.Reason);
        }

        throw new InvalidOperationException();
    }
}
