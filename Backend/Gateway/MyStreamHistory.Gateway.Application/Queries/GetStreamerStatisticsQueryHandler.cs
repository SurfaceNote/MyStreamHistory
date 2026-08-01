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
                TwitchUserId = request.TwitchUserId,
                Period = request.Period
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
            var configuredPlaythroughs = playthroughSettingsResponse.Success!.Settings.Playthroughs;
            var viewerCounts = new Dictionary<Guid, int>();

            if (configuredPlaythroughs.Count > 0)
            {
                var viewerCountsResponse = await _bus.SendRequestAsync<
                    GetUniqueViewerCountsRequestContract,
                    GetUniqueViewerCountsResponseContract,
                    BaseFailedResponseContract>(
                    new GetUniqueViewerCountsRequestContract
                    {
                        Playthroughs = configuredPlaythroughs.Select(playthrough =>
                            new PlaythroughViewerCountRequestContract
                            {
                                PlaythroughId = playthrough.Id,
                                StreamCategoryIds = playthrough.StreamCategories
                                    .Select(sc => sc.StreamCategoryId)
                                    .Distinct()
                                    .ToList()
                            }).ToList()
                    },
                    cancellationToken);

                if (viewerCountsResponse.IsSuccess)
                {
                    viewerCounts = viewerCountsResponse.Success!.Counts
                        .ToDictionary(c => c.PlaythroughId, c => c.UniqueViewerCount);
                }
            }

            foreach (var playthrough in configuredPlaythroughs)
            {
                var orderedSegments = playthrough.StreamCategories
                    .OrderBy(sc => sc.CategoryStartedAt)
                    .ToList();

                var uniqueViewersCount = viewerCounts.GetValueOrDefault(playthrough.Id);

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
                .ToList(),
            Dashboard = new StreamerDashboardDto
            {
                Period = response.Success!.Dashboard.Period,
                From = response.Success!.Dashboard.From,
                To = response.Success!.Dashboard.To,
                TotalStreamsCount = response.Success!.Dashboard.TotalStreamsCount,
                TotalUniqueGamesCount = response.Success!.Dashboard.TotalUniqueGamesCount,
                TotalStreamedHours = response.Success!.Dashboard.TotalStreamedHours,
                StreamedHoursByDay = response.Success!.Dashboard.StreamedHoursByDay.Select(p => new TimeSeriesPointDto
                {
                    Date = p.Date,
                    Value = p.Value
                }).ToList(),
                StreamedHoursByWeek = response.Success!.Dashboard.StreamedHoursByWeek.Select(p => new TimeSeriesPointDto
                {
                    Date = p.Date,
                    Value = p.Value
                }).ToList(),
                ChatPointsByDay = response.Success!.Dashboard.ChatPointsByDay.Select(p => new TimeSeriesPointDto
                {
                    Date = p.Date,
                    Value = p.Value
                }).ToList(),
                ViewerCountByDay = response.Success!.Dashboard.ViewerCountByDay.Select(p => new TimeSeriesPointDto
                {
                    Date = p.Date,
                    Value = p.Value
                }).ToList(),
                TopCategories = response.Success!.Dashboard.TopCategories.Select(c => new CategoryAnalyticsDto
                {
                    TwitchCategoryId = c.TwitchCategoryId,
                    TwitchId = c.TwitchId,
                    Name = c.Name,
                    BoxArtUrl = c.BoxArtUrl,
                    TotalHours = c.TotalHours,
                    StreamsCount = c.StreamsCount,
                    AverageViewers = c.AverageViewers
                }).ToList()
            }
        };
    }
}

