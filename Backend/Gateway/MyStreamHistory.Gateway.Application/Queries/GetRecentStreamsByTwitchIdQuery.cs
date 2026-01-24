using MediatR;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetRecentStreamsByTwitchIdQuery : IRequest<List<StreamSessionDto>>
{
    public int TwitchUserId { get; set; }
    public int Count { get; set; } = 10;
}

