using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetStreamerStatisticsQuery : IRequest<StreamerStatisticsDto>
{
    public int TwitchUserId { get; set; }
}

