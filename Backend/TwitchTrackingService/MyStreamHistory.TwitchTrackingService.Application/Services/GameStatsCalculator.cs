using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Services;

/// <summary>
/// Builds game statistics from category segments. The game page and the games catalog both use it,
/// so the numbers they show always agree.
/// </summary>
public static class GameStatsCalculator
{
    public static readonly TimeSpan RecentPeriod = TimeSpan.FromDays(30);

    /// <summary>
    /// A live stream not confirmed for this long is treated as not live: polling confirms live streams every
    /// minute and ends missing ones after a 10-minute grace period.
    /// </summary>
    public static readonly TimeSpan LiveConfirmationTimeout = TimeSpan.FromMinutes(15);

    public static IReadOnlyList<GameStats> Build(
        IEnumerable<GameSegmentRow> segments,
        IEnumerable<GamePlaythroughRow> playthroughs,
        DateTime now)
    {
        var playthroughsByGameAndUser = playthroughs
            .GroupBy(p => (p.TwitchCategoryId, p.TwitchUserId))
            .ToDictionary(g => g.Key, g => g.ToList());
        var recentFrom = now - RecentPeriod;

        return segments
            .GroupBy(s => s.TwitchCategoryId)
            .Select(game => BuildGame(game.ToList(), playthroughsByGameAndUser, now, recentFrom))
            .ToList();
    }

    private static GameStats BuildGame(
        List<GameSegmentRow> segments,
        Dictionary<(Guid, int), List<GamePlaythroughRow>> playthroughsByGameAndUser,
        DateTime now,
        DateTime recentFrom)
    {
        var category = segments[0];
        var streamers = segments
            .GroupBy(s => s.TwitchUserId)
            .Select(user => BuildStreamer(
                user.ToList(),
                playthroughsByGameAndUser.GetValueOrDefault((category.TwitchCategoryId, user.Key)),
                now))
            .OrderByDescending(s => s.IsLiveWithGame)
            .ThenByDescending(s => s.LastStreamedAt)
            .ToList();

        var completedCount = streamers.Count(s =>
            playthroughsByGameAndUser.GetValueOrDefault((category.TwitchCategoryId, s.TwitchUserId))?
                .Any(p => p.Status == PlaythroughStatus.Completed) == true);

        var summary = new GameSummary(
            category.TwitchCategoryId,
            category.CategoryTwitchId,
            category.Name,
            category.BoxArtUrl,
            category.IgdbId,
            TotalHours: segments.Sum(s => Duration(s, now, DateTime.MinValue)),
            HoursLast30Days: segments.Sum(s => Duration(s, now, recentFrom)),
            StreamsCount: segments.Select(s => s.StreamSessionId).Distinct().Count(),
            ChannelsCount: streamers.Count,
            Languages: streamers
                .Where(s => s.Language != null)
                .GroupBy(s => s.Language!)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => g.Key)
                .ToList(),
            LiveCount: streamers.Count(s => s.IsLiveWithGame),
            CompletedCount: completedCount,
            FirstStreamedAt: streamers.Min(s => s.FirstStreamedAt),
            LastStreamedAt: streamers.Max(s => s.LastStreamedAt));

        return new GameStats(summary, streamers);
    }

    private static GameStreamerStats BuildStreamer(
        List<GameSegmentRow> segments,
        List<GamePlaythroughRow>? playthroughs,
        DateTime now)
    {
        var latest = segments.MaxBy(s => s.SessionStartedAt)!;
        var streams = segments
            .GroupBy(s => s.StreamSessionId)
            .Select(g => g.First())
            .ToList();
        var language = streams
            .Where(s => s.Language != null)
            .GroupBy(s => s.Language!)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Max(s => s.SessionStartedAt))
            .Select(g => g.Key)
            .FirstOrDefault();
        var playthrough = playthroughs?.MaxBy(p => p.UpdatedAt);

        return new GameStreamerStats(
            latest.TwitchUserId,
            latest.StreamerLogin,
            latest.StreamerDisplayName,
            latest.StreamerAvatarUrl,
            language,
            Hours: segments.Sum(s => Duration(s, now, DateTime.MinValue)),
            StreamsCount: streams.Count,
            FirstStreamedAt: segments.Min(s => s.StartedAt),
            LastStreamedAt: segments.Max(s => s.StartedAt),
            IsLiveWithGame: segments.Any(s => IsOpenOnLiveStream(s, now)),
            Playthrough: playthrough == null
                ? null
                : new GamePlaythroughSummary(playthrough.Status, playthrough.Title, playthrough.StreamsCount));
    }

    /// <summary>Hours of the segment inside [<paramref name="from"/>, <paramref name="now"/>].</summary>
    private static double Duration(GameSegmentRow segment, DateTime now, DateTime from)
    {
        var end = SegmentEnd(segment, now);
        if (end == null)
        {
            return 0;
        }

        var start = segment.StartedAt > from ? segment.StartedAt : from;
        var finish = end.Value < now ? end.Value : now;
        return finish > start ? (finish - start).TotalHours : 0;
    }

    /// <summary>
    /// A closed segment ends at its own end. An open segment of a live stream runs until the stream was last
    /// confirmed live (or now, before the first confirmation), so a stream stuck as live does not keep
    /// growing. An open segment of an ended stream ends with the stream.
    /// </summary>
    private static DateTime? SegmentEnd(GameSegmentRow segment, DateTime now) =>
        segment.EndedAt
        ?? (segment.SessionIsLive ? segment.SessionLastConfirmedLiveAt ?? now : segment.SessionEndedAt);

    private static bool IsOpenOnLiveStream(GameSegmentRow segment, DateTime now) =>
        segment.SessionIsLive
        && segment.EndedAt == null
        && (segment.SessionLastConfirmedLiveAt == null || now - segment.SessionLastConfirmedLiveAt <= LiveConfirmationTimeout);
}
