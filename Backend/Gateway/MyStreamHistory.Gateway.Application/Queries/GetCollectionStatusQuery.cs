using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.ProductActivity;

namespace MyStreamHistory.Gateway.Application.Queries;

public sealed record GetCollectionStatusQuery(int TwitchUserId) : IRequest<GetCollectionStatusResponseContract>;

public sealed class GetCollectionStatusQueryHandler(ITransportBus bus)
    : IRequestHandler<GetCollectionStatusQuery, GetCollectionStatusResponseContract>
{
    public async Task<GetCollectionStatusResponseContract> Handle(GetCollectionStatusQuery request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<GetCollectionStatusRequestContract, GetCollectionStatusResponseContract>(
            new GetCollectionStatusRequestContract { TwitchUserId = request.TwitchUserId },
            cancellationToken);

        return response.Success ?? throw new InvalidOperationException("Collection status is unavailable");
    }
}
