using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Requests;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Responses;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;

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

        if (!response.IsSuccess)
        {
            throw new InvalidOperationException($"Failed to get streamer statistics: {response.Failure?.Reason}");
        }

        var playthroughSettingsResponse = await _bus.SendRequestAsync<
            GetPlaythroughSettingsRequestContract,
            GetPlaythroughSettingsResponseContract,
            BaseFailedResponseContract>(
            new GetPlaythroughSettingsRequestContract
            {
                TwitchUserId = request.TwitchUserId
            },
            cancellationToken);

        var playthroughs = new List<PlaythroughStatisticsDto>();

        if (playthroughSettingsResponse.IsSuccess)
        {
            foreach (var playthrough in playthroughSettingsResponse.Success!.Settings.Playthroughs)
            {
                var orderedSegments = playthrough.StreamCategories
                    .OrderBy(sc => sc.CategoryStartedAt)
                    .ToList();

                var uniqueViewersCount = 0;
                var streamCategoryIds = orderedSegments
                    .Select(sc => sc.StreamCategoryId)
                    .Distinct()
                    .ToList();

                if (streamCategoryIds.Count > 0)
                {
                    var viewersResponse = await _bus.SendRequestAsync<
                        GetStreamViewersRequestContract,
                        GetStreamViewersResponseContract,
                        BaseFailedResponseContract>(
                        new GetStreamViewersRequestContract
                        {
                            StreamSessionId = orderedSegments[0].StreamSessionId,
                            StreamCategoryIds = streamCategoryIds
                        },
                        cancellationToken);

                    if (viewersResponse.IsSuccess && viewersResponse.Success!.Success)
                    {
                        uniqueViewersCount = viewersResponse.Success.Viewers
                            .Select(v => v.ViewerId)
                            .Distinct()
                            .Count();
                    }
                }

                var totalHours = orderedSegments
                    .Where(sc => sc.CategoryEndedAt.HasValue)
                    .Sum(sc => (sc.CategoryEndedAt!.Value - sc.CategoryStartedAt).TotalHours);

                playthroughs.Add(new PlaythroughStatisticsDto
                {
                    PlaythroughId = playthrough.Id,
                    Title = playthrough.Title,
                    Status = playthrough.Status,
                    TwitchCategoryId = playthrough.TwitchCategoryId,
                    TwitchCategoryTwitchId = playthrough.TwitchCategoryTwitchId,
                    GameName = playthrough.GameName,
                    BoxArtUrl = playthrough.BoxArtUrl,
                    FirstStreamStartedAt = orderedSegments.FirstOrDefault()?.CategoryStartedAt,
                    LastStreamStartedAt = orderedSegments.LastOrDefault()?.CategoryStartedAt,
                    UniqueViewersCount = uniqueViewersCount,
                    TotalHours = Math.Round(totalHours, 1)
                });
            }
        }

        return new StreamerStatisticsDto
        {
            TotalStreamsCount = response.Success!.TotalStreamsCount,
            TotalUniqueGamesCount = response.Success!.TotalUniqueGamesCount,
            TotalStreamedHours = response.Success!.TotalStreamedHours,
            AllGamesTotalHours = response.Success!.AllGamesTotalHours,
            Categories = response.Success!.Categories.Select(c => new CategoryStatisticsDto
            {
                TwitchCategoryId = c.TwitchCategoryId,
                TwitchId = c.TwitchId,
                Name = c.Name,
                BoxArtUrl = c.BoxArtUrl,
                IgdbId = c.IgdbId,
                TotalHours = c.TotalHours
            }).ToList(),
            Playthroughs = playthroughs
                .OrderByDescending(p => p.LastStreamStartedAt ?? DateTime.MinValue)
                .ToList()
        };
    }
}

