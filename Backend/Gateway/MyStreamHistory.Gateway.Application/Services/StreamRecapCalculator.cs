using MyStreamHistory.Gateway.Application.DTOs;
using PlaythroughDto = MyStreamHistory.Shared.Base.Contracts.Playthroughs.PlaythroughDto;

namespace MyStreamHistory.Gateway.Application.Services;

public record RecapStream(Guid Id, DateTime StartedAt, DateTime? EndedAt, bool IsLive);

public record RecapAudienceMember(
    string TwitchUserId,
    string DisplayName,
    string Login,
    string? ProfileImageUrl,
    int MinutesWatched,
    decimal ChatPoints);

/// <summary>Builds a stream recap from data that the services already store. Pure, so it is unit tested.</summary>
public static class StreamRecapCalculator
{
    /// <summary>Averages compare the stream with this many earlier finished streams.</summary>
    public const int ComparisonStreams = 10;
    /// <summary>An account that skipped at least this many streams in a row and came back is "returning".</summary>
    public const int ReturningAfterMissedStreams = 3;
    /// <summary>An account in chat on at least this many streams in a row is shown as a regular.</summary>
    public const int MinimumStreak = 3;
    public const int ListLimit = 20;
    public const int HighlightLimit = 5;

    public static StreamRecapDto Build(
        RecapStream stream,
        IReadOnlyList<decimal?> audienceAverages,
        IReadOnlyList<RecapAudienceMember>? audience,
        IReadOnlyList<RecapStream> earlierStreams,
        IReadOnlyDictionary<string, HashSet<Guid>> appearances,
        IReadOnlyDictionary<Guid, int> earlierAudienceCounts,
        IReadOnlyList<PlaythroughDto> playthroughs,
        DateTime now)
    {
        var previous = earlierStreams
            .Where(s => s.Id != stream.Id && s.StartedAt < stream.StartedAt)
            .OrderByDescending(s => s.StartedAt)
            .ToList();
        var comparison = previous.Where(s => !s.IsLive && s.EndedAt.HasValue).Take(ComparisonStreams).ToList();
        var counts = comparison
            .Where(s => earlierAudienceCounts.ContainsKey(s.Id))
            .Select(s => earlierAudienceCounts[s.Id])
            .ToList();
        var peaks = audienceAverages.Where(v => v.HasValue).Select(v => v!.Value).ToList();

        var recap = new StreamRecapDto
        {
            StreamSessionId = stream.Id,
            IsLive = stream.IsLive,
            ViewerDataAvailable = audience != null,
            PreviousStreamsCount = previous.Count,
            ComparisonStreamsCount = comparison.Count,
            DurationMinutes = Minutes(stream.StartedAt, stream.EndedAt ?? now),
            AverageDurationMinutes = comparison.Count > 0
                ? (int)Math.Round(comparison.Average(s => Minutes(s.StartedAt, s.EndedAt!.Value)))
                : null,
            PeakViewers = peaks.Count > 0 ? (int)Math.Round(peaks.Max()) : null,
            AverageViewers = peaks.Count > 0 ? Math.Round((double)peaks.Average(), 1) : null,
            ChatAccounts = audience?.Count ?? 0,
            AverageChatAccounts = counts.Count > 0 ? Math.Round(counts.Average(), 1) : null,
            Playthroughs = BuildPlaythroughs(stream.Id, playthroughs)
        };

        if (audience == null || audience.Count == 0)
        {
            return recap;
        }

        var newViewers = new List<RecapViewerDto>();
        var returning = new List<RecapViewerDto>();
        var regulars = new List<RecapViewerDto>();

        foreach (var member in audience)
        {
            var seen = appearances.GetValueOrDefault(member.TwitchUserId);
            var lastSeenIndex = seen == null ? -1 : previous.FindIndex(s => seen.Contains(s.Id));

            if (previous.Count > 0 && lastSeenIndex < 0)
            {
                newViewers.Add(Map(member));
            }
            else if (lastSeenIndex >= ReturningAfterMissedStreams)
            {
                var viewer = Map(member);
                viewer.MissedStreams = lastSeenIndex;
                returning.Add(viewer);
            }
            else if (lastSeenIndex == 0)
            {
                var streak = 1 + previous.TakeWhile(s => seen!.Contains(s.Id)).Count();
                if (streak >= MinimumStreak)
                {
                    var viewer = Map(member);
                    viewer.Streak = streak;
                    regulars.Add(viewer);
                }
            }
        }

        recap.NewCount = newViewers.Count;
        recap.ReturningCount = returning.Count;
        recap.NewViewers = newViewers.OrderByDescending(v => v.MinutesWatched).ThenByDescending(v => v.ChatPoints).Take(ListLimit).ToList();
        recap.ReturningViewers = returning.OrderByDescending(v => v.MissedStreams).ThenByDescending(v => v.MinutesWatched).Take(ListLimit).ToList();
        recap.StreakViewers = regulars.OrderByDescending(v => v.Streak).ThenByDescending(v => v.MinutesWatched).Take(HighlightLimit).ToList();
        recap.TopChatters = audience
            .Where(m => m.ChatPoints > 0)
            .OrderByDescending(m => m.ChatPoints)
            .ThenByDescending(m => m.MinutesWatched)
            .Take(HighlightLimit)
            .Select(Map)
            .ToList();
        return recap;
    }

    private static List<RecapPlaythroughDto> BuildPlaythroughs(Guid streamId, IReadOnlyList<PlaythroughDto> playthroughs)
    {
        var result = new List<RecapPlaythroughDto>();
        foreach (var playthrough in playthroughs)
        {
            var streams = playthrough.StreamCategories
                .GroupBy(sc => sc.StreamSessionId)
                .Select(group => new { Id = group.Key, StartedAt = group.Min(sc => sc.StreamStartedAt) })
                .OrderBy(s => s.StartedAt)
                .Select(s => s.Id)
                .ToList();
            var index = streams.IndexOf(streamId);
            if (index < 0) continue;

            result.Add(new RecapPlaythroughDto
            {
                PlaythroughId = playthrough.Id,
                GameName = playthrough.GameName,
                BoxArtUrl = playthrough.BoxArtUrl,
                Status = playthrough.Status,
                StreamNumber = index + 1,
                StreamsCount = streams.Count
            });
        }

        return result;
    }

    private static RecapViewerDto Map(RecapAudienceMember member) => new()
    {
        TwitchUserId = member.TwitchUserId,
        DisplayName = member.DisplayName,
        Login = member.Login,
        ProfileImageUrl = member.ProfileImageUrl,
        MinutesWatched = member.MinutesWatched,
        ChatPoints = member.ChatPoints
    };

    private static int Minutes(DateTime from, DateTime to) => Math.Max(0, (int)Math.Round((to - from).TotalMinutes));
}
