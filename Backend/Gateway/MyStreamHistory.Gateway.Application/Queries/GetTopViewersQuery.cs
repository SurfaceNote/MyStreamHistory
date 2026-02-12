using MediatR;
using MyStreamHistory.Shared.Base.Contracts.Viewers;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetTopViewersQuery : IRequest<List<ViewerStatsDto>>
{
    public string StreamerTwitchUserId { get; set; } = string.Empty;
    public int Limit { get; set; } = 100;
}

