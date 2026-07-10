using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.Viewers;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class GetViewerWatchHistoryConsumer(
    IStreamCategoryRepository streamCategoryRepository,
    ILogger<GetViewerWatchHistoryConsumer> logger)
    : IConsumer<GetViewerWatchHistoryRequestContract>
{
    public async Task Consume(ConsumeContext<GetViewerWatchHistoryRequestContract> context)
    {
        var request = context.Message;

        try
        {
            if (request.CategoryWatches.Count == 0)
            {
                await context.RespondAsync(new GetViewerWatchHistoryResponseContract { Success = true });
                return;
            }

            var watchesByCategoryId = request.CategoryWatches
                .GroupBy(watch => watch.StreamCategoryId)
                .ToDictionary(group => group.Key, group => group.Last());

            var streamCategories = await streamCategoryRepository.GetByIdsAsync(
                watchesByCategoryId.Keys.ToList(),
                context.CancellationToken);

            var matchedCategories = streamCategories
                .Where(category => category.StreamSession.TwitchUserId == request.StreamerTwitchUserId)
                .Where(category => watchesByCategoryId.ContainsKey(category.Id))
                .ToList();

            var streamHistory = matchedCategories
                .GroupBy(category => category.StreamSessionId)
                .Select(group => BuildStreamHistory(group, watchesByCategoryId))
                .OrderByDescending(stream => stream.StartedAt)
                .ToList();

            var totalBroadcastMinutes = streamHistory.Sum(stream => stream.DurationMinutes);
            var totalWatchedMinutes = streamHistory.Sum(stream => stream.MinutesWatched);

            var favoriteCategories = matchedCategories
                .GroupBy(category => new
                {
                    category.TwitchCategory.TwitchId,
                    category.TwitchCategory.Name,
                    category.TwitchCategory.BoxArtUrl
                })
                .Select(group => new ViewerFavoriteCategoryDto
                {
                    TwitchCategoryId = group.Key.TwitchId,
                    Name = group.Key.Name,
                    BoxArtUrl = group.Key.BoxArtUrl,
                    MinutesWatched = group.Sum(category => watchesByCategoryId[category.Id].MinutesWatched),
                    StreamsCount = group.Select(category => category.StreamSessionId).Distinct().Count()
                })
                .OrderByDescending(category => category.MinutesWatched)
                .Take(5)
                .ToList();

            var matchingWatches = matchedCategories
                .Select(category => watchesByCategoryId[category.Id])
                .ToList();

            await context.RespondAsync(new GetViewerWatchHistoryResponseContract
            {
                Success = true,
                StreamsWatched = streamHistory.Count,
                FirstWatchedAt = streamHistory.Count > 0 ? streamHistory.Min(stream => stream.StartedAt) : null,
                LastWatchedAt = matchingWatches.Count > 0 ? matchingWatches.Max(watch => watch.LastUpdatedAt) : null,
                WatchedPercentage = CalculatePercentage(totalWatchedMinutes, totalBroadcastMinutes),
                FavoriteCategories = favoriteCategories,
                RecentStreams = streamHistory.Take(Math.Clamp(request.RecentStreamsLimit, 1, 50)).ToList()
            });
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to build viewer history for streamer {StreamerTwitchUserId}",
                request.StreamerTwitchUserId);

            await context.RespondAsync(new GetViewerWatchHistoryResponseContract
            {
                Success = false,
                Error = exception.Message
            });
        }
    }

    private static ViewerStreamHistoryDto BuildStreamHistory(
        IGrouping<Guid, StreamCategory> categoryGroup,
        IReadOnlyDictionary<Guid, ViewerCategoryWatchDto> watchesByCategoryId)
    {
        var session = categoryGroup.First().StreamSession;
        var endedAt = session.EndedAt ?? DateTime.UtcNow;
        var durationMinutes = Math.Max(1, (int)Math.Ceiling((endedAt - session.StartedAt).TotalMinutes));
        var rawWatchedMinutes = categoryGroup.Sum(category => watchesByCategoryId[category.Id].MinutesWatched);
        var watchedMinutes = Math.Min(rawWatchedMinutes, durationMinutes);

        return new ViewerStreamHistoryDto
        {
            StreamSessionId = session.Id,
            StreamTitle = session.StreamTitle,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            DurationMinutes = durationMinutes,
            MinutesWatched = watchedMinutes,
            WatchedPercentage = CalculatePercentage(watchedMinutes, durationMinutes),
            ChatPoints = categoryGroup.Sum(category => watchesByCategoryId[category.Id].ChatPoints),
            Categories = categoryGroup
                .Select(category => category.TwitchCategory.Name)
                .Distinct()
                .ToList()
        };
    }

    private static decimal CalculatePercentage(int watchedMinutes, int durationMinutes)
    {
        if (durationMinutes <= 0)
        {
            return 0;
        }

        return Math.Round(Math.Min(100m, (decimal)watchedMinutes / durationMinutes * 100m), 1);
    }
}
