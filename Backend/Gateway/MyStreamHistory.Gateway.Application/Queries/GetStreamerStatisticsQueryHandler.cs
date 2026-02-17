using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetStreamerStatisticsQueryHandler : IRequestHandler<GetStreamerStatisticsQuery, StreamerStatisticsDto>
{
    private readonly ITransportBus _bus;

    public GetStreamerStatisticsQueryHandler(ITransportBus bus)
    {
        _bus = bus;
    }

    public async Task<StreamerStatisticsDto> Handle(GetStreamerStatisticsQuery request, CancellationToken cancellationToken)
    {
        var response = await _bus.SendRequestAsync<
            GetStreamerStatisticsRequestContract,
            GetStreamerStatisticsResponseContract,
            BaseFailedResponseContract>(
            new GetStreamerStatisticsRequestContract
            {
                TwitchUserId = request.TwitchUserId
            },
            cancellationToken);

        if (response.IsSuccess)
        {
            return new StreamerStatisticsDto
            {
                TotalStreamsCount = response.Success!.TotalStreamsCount,
                TotalUniqueGamesCount = response.Success!.TotalUniqueGamesCount,
                TotalStreamedHours = response.Success!.TotalStreamedHours,
                Categories = response.Success!.Categories.Select(c => new CategoryStatisticsDto
                {
                    TwitchCategoryId = c.TwitchCategoryId,
                    TwitchId = c.TwitchId,
                    Name = c.Name,
                    BoxArtUrl = c.BoxArtUrl,
                    IgdbId = c.IgdbId,
                    TotalHours = c.TotalHours
                }).ToList()
            };
        }

        throw new InvalidOperationException($"Failed to get streamer statistics: {response.Failure?.Reason}");
    }
}

