using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Viewers;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;
using MyStreamHistory.Shared.Base.Exceptions;
using MyStreamHistory.Shared.Base.Error;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetStreamerViewerStatsQueryHandler(ITransportBus bus)
    : IRequestHandler<GetStreamerViewerStatsQuery, ViewerStatsDto>
{
    public async Task<ViewerStatsDto> Handle(
        GetStreamerViewerStatsQuery request,
        CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            GetStreamerViewerStatsRequestContract,
            GetStreamerViewerStatsResponseContract,
            BaseFailedResponseContract>(
                new GetStreamerViewerStatsRequestContract
                {
                    StreamerTwitchUserId = request.StreamerTwitchUserId,
                    ViewerTwitchUserId = request.ViewerTwitchUserId
                },
                cancellationToken);

        if (response.IsFailure)
        {
            throw new AppException(response.Failure!.Reason);
        }

        if (!response.Success!.Success || response.Success.Stats is null)
        {
            var error = response.Success.Error;
            throw error == ErrorCodes.NotFound
                ? new AppException(ErrorCodes.NotFound, "Viewer statistics were not found")
                : new AppException(ErrorCodes.InternalError, error ?? "Failed to get viewer statistics");
        }

        if (!int.TryParse(request.StreamerTwitchUserId, out var streamerTwitchUserId))
        {
            throw new AppException(ErrorCodes.InternalError, "Invalid streamer Twitch ID");
        }

        var historyResponse = await bus.SendRequestAsync<
            GetViewerWatchHistoryRequestContract,
            GetViewerWatchHistoryResponseContract,
            BaseFailedResponseContract>(
                new GetViewerWatchHistoryRequestContract
                {
                    StreamerTwitchUserId = streamerTwitchUserId,
                    RecentStreamsLimit = 10,
                    CategoryWatches = response.Success.CategoryWatches
                },
                cancellationToken);

        if (historyResponse.IsFailure)
        {
            throw new AppException(historyResponse.Failure!.Reason);
        }

        if (!historyResponse.Success!.Success)
        {
            throw new AppException(
                ErrorCodes.InternalError,
                historyResponse.Success.Error ?? "Failed to get viewer watch history");
        }

        var stats = response.Success.Stats;
        stats.StreamsWatched = historyResponse.Success.StreamsWatched;
        stats.FirstWatchedAt = historyResponse.Success.FirstWatchedAt;
        stats.LastWatchedAt = historyResponse.Success.LastWatchedAt;
        stats.WatchedPercentage = historyResponse.Success.WatchedPercentage;
        stats.FavoriteCategories = historyResponse.Success.FavoriteCategories;
        stats.RecentStreams = historyResponse.Success.RecentStreams;

        return stats;
    }
}
