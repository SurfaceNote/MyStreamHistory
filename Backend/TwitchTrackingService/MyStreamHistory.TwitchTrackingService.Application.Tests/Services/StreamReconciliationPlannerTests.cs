using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Application.Services;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Application.Tests.Services;

public class StreamReconciliationPlannerTests
{
    [Fact]
    public void Build_ChangedStreamId_ClassifiesBroadcastAsRestarted()
    {
        var startedAt = DateTime.UtcNow.AddHours(-1);
        var session = CreateSession(1, "old-stream", startedAt);
        var twitchStream = CreateStream(1, "new-stream", startedAt);

        var plan = StreamReconciliationPlanner.Build([session], [twitchStream], [1]);

        Assert.Contains(1, plan.RestartedUserIds);
        Assert.DoesNotContain(1, plan.ConfirmedUserIds);
        Assert.Empty(plan.MissingSessions);
        Assert.Empty(plan.DiscoveredUserIds);
    }

    [Fact]
    public void Build_NewerStartedAtWithoutStoredStreamId_ClassifiesBroadcastAsRestarted()
    {
        var session = CreateSession(1, null, DateTime.UtcNow.AddHours(-1));
        var twitchStream = CreateStream(1, "new-stream", DateTime.UtcNow.AddMinutes(-1));

        var plan = StreamReconciliationPlanner.Build([session], [twitchStream], [1]);

        Assert.Contains(1, plan.RestartedUserIds);
    }

    [Fact]
    public void Build_TrackedLiveStreamWithoutLocalSession_ClassifiesItAsDiscovered()
    {
        var twitchStream = CreateStream(2, "stream", DateTime.UtcNow.AddMinutes(-5));

        var plan = StreamReconciliationPlanner.Build([], [twitchStream], [2]);

        Assert.Equal([2], plan.DiscoveredUserIds);
    }

    [Fact]
    public void Build_UntrackedLiveStream_IsNotDiscovered()
    {
        var twitchStream = CreateStream(2, "stream", DateTime.UtcNow.AddMinutes(-5));

        var plan = StreamReconciliationPlanner.Build([], [twitchStream], []);

        Assert.Empty(plan.DiscoveredUserIds);
    }

    [Fact]
    public void Build_AbsentLocalSession_IsMissingWhileMatchingSessionIsConfirmed()
    {
        var startedAt = DateTime.UtcNow.AddHours(-1);
        var confirmed = CreateSession(1, "stream-1", startedAt);
        var missing = CreateSession(2, "stream-2", startedAt);
        var twitchStream = CreateStream(1, "stream-1", startedAt);

        var plan = StreamReconciliationPlanner.Build([confirmed, missing], [twitchStream], [1, 2]);

        Assert.Equal([1], plan.ConfirmedUserIds);
        Assert.Equal(missing.Id, Assert.Single(plan.MissingSessions).Id);
    }

    private static StreamSession CreateSession(int userId, string? streamId, DateTime startedAt)
    {
        return new StreamSession
        {
            TwitchUserId = userId,
            StreamId = streamId,
            StreamerLogin = $"user-{userId}",
            StreamerDisplayName = $"User {userId}",
            StartedAt = startedAt,
            IsLive = true
        };
    }

    private static TwitchStreamDto CreateStream(int userId, string streamId, DateTime startedAt)
    {
        return new TwitchStreamDto
        {
            Id = streamId,
            UserId = userId.ToString(),
            UserLogin = $"user-{userId}",
            UserName = $"User {userId}",
            StartedAt = startedAt,
            Type = "live",
            GameId = string.Empty,
            GameName = string.Empty,
            Title = string.Empty
        };
    }
}
