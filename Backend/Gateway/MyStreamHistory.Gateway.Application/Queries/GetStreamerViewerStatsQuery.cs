using MediatR;
using MyStreamHistory.Shared.Base.Contracts.Viewers;

namespace MyStreamHistory.Gateway.Application.Queries;

public record GetStreamerViewerStatsQuery(
    string StreamerTwitchUserId,
    string ViewerTwitchUserId) : IRequest<ViewerStatsDto>;
