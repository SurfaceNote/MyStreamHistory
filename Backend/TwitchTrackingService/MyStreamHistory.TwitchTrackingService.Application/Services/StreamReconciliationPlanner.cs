using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Services;

public sealed class StreamReconciliationPlan
{
    public required IReadOnlyDictionary<int, TwitchStreamDto> StreamsByUserId { get; init; }
    public required IReadOnlyDictionary<int, StreamSession> SessionsByUserId { get; init; }
    public required IReadOnlySet<int> RestartedUserIds { get; init; }
    public required IReadOnlyList<int> ConfirmedUserIds { get; init; }
    public required IReadOnlyList<StreamSession> MissingSessions { get; init; }
    public required IReadOnlyList<int> DiscoveredUserIds { get; init; }
}

public static class StreamReconciliationPlanner
{
    private static readonly TimeSpan StartedAtTolerance = TimeSpan.FromSeconds(5);

    public static StreamReconciliationPlan Build(
        IReadOnlyCollection<StreamSession> activeSessions,
        IReadOnlyCollection<TwitchStreamDto> streams,
        IReadOnlyCollection<int> trackedUserIds)
    {
        var streamsByUserId = new Dictionary<int, TwitchStreamDto>();
        foreach (var stream in streams)
        {
            if (!int.TryParse(stream.UserId, out var userId))
            {
                throw new InvalidDataException(
                    $"Twitch returned an invalid stream user ID: '{stream.UserId}'.");
            }

            streamsByUserId[userId] = stream;
        }

        var sessionsByUserId = activeSessions
            .GroupBy(session => session.TwitchUserId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(session => session.StartedAt).First());
        var trackedUserIdSet = trackedUserIds.ToHashSet();
        var restartedUserIds = streamsByUserId
            .Where(pair => sessionsByUserId.TryGetValue(pair.Key, out var session)
                && IsDifferentBroadcast(session, pair.Value, pair.Value.StartedAt))
            .Select(pair => pair.Key)
            .ToHashSet();

        return new StreamReconciliationPlan
        {
            StreamsByUserId = streamsByUserId,
            SessionsByUserId = sessionsByUserId,
            RestartedUserIds = restartedUserIds,
            ConfirmedUserIds = streamsByUserId.Keys
                .Where(sessionsByUserId.ContainsKey)
                .Where(userId => !restartedUserIds.Contains(userId))
                .ToArray(),
            MissingSessions = sessionsByUserId.Values
                .Where(session => !streamsByUserId.ContainsKey(session.TwitchUserId))
                .ToArray(),
            DiscoveredUserIds = streamsByUserId.Keys
                .Where(userId => !sessionsByUserId.ContainsKey(userId))
                .Where(trackedUserIdSet.Contains)
                .ToArray()
        };
    }

    public static bool IsDifferentBroadcast(
        StreamSession activeSession,
        TwitchStreamDto? twitchStream,
        DateTime observedStartedAt)
    {
        if (twitchStream != null
            && !string.IsNullOrWhiteSpace(activeSession.StreamId)
            && !string.Equals(activeSession.StreamId, twitchStream.Id, StringComparison.Ordinal))
        {
            return true;
        }

        var twitchStartedAt = twitchStream?.StartedAt ?? observedStartedAt;
        return twitchStartedAt > activeSession.StartedAt + StartedAtTolerance;
    }
}
