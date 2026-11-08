using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyStreamHistory.Shared.Base.Contracts.ProductActivity;
using MyStreamHistory.TwitchTrackingService.Api.Consumers;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Repositories;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Api.Tests;

public class ProductActivityTests
{
    [Fact]
    public async Task RecentStreams_WithBeforeCursor_ReturnOnlyOlderStreamsNewestFirst()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;
        db.StreamSessions.AddRange(
            Session(now.AddDays(-1)),
            Session(now.AddDays(-2)),
            Session(now.AddDays(-3)),
            Session(now.AddDays(-4)));
        await db.SaveChangesAsync();
        var repository = new StreamSessionRepository(db);

        var firstPage = await repository.GetRecentStreamsByTwitchUserIdAsync(123, 2);
        var secondPage = await repository.GetRecentStreamsByTwitchUserIdAsync(123, 2, before: firstPage[^1].StartedAt);

        Assert.Equal(2, firstPage.Count);
        Assert.Equal(2, secondPage.Count);
        Assert.All(secondPage, stream => Assert.True(stream.StartedAt < firstPage[^1].StartedAt));
        Assert.True(secondPage[0].StartedAt > secondPage[1].StartedAt);
        Assert.Empty(await repository.GetRecentStreamsByTwitchUserIdAsync(123, 2, before: secondPage[^1].StartedAt));
    }

    [Fact]
    public async Task CollectionSnapshot_ReportsOwnStreamsAndSurfaces()
    {
        await using var db = CreateDb();
        var now = DateTime.UtcNow;
        db.StreamSessions.AddRange(Session(now.AddDays(-3)), Session(now.AddHours(-1), live: true));
        db.StreamSessions.Add(Session(now, twitchUserId: 999));
        db.ChannelActivityDays.AddRange(
            Activity(123, ChannelActivityKinds.Overlay, now.AddDays(-2)),
            Activity(123, ChannelActivityKinds.Overlay, now.AddMinutes(-5)),
            Activity(123, ChannelActivityKinds.Visit, now),
            Activity(999, ChannelActivityKinds.Panel, now));
        await db.SaveChangesAsync();

        var snapshot = await new ChannelActivityRepository(db).GetCollectionSnapshotAsync(123);

        Assert.Equal(2, snapshot.StreamsCount);
        Assert.True(snapshot.IsLive);
        Assert.Equal(now.AddHours(-1), snapshot.LastStreamStartedAt);
        Assert.Equal(now.AddMinutes(-5), snapshot.LastOverlaySeenAt);
        Assert.Null(snapshot.LastPanelSeenAt);
    }

    [Fact]
    public async Task CollectionSnapshot_ForNewChannel_IsEmpty()
    {
        await using var db = CreateDb();

        var snapshot = await new ChannelActivityRepository(db).GetCollectionSnapshotAsync(123);

        Assert.Equal(0, snapshot.StreamsCount);
        Assert.False(snapshot.IsLive);
        Assert.Null(snapshot.LastStreamStartedAt);
        Assert.Null(snapshot.LastOverlaySeenAt);
    }

    [Theory]
    [InlineData(123, "unknown")]
    [InlineData(0, ChannelActivityKinds.Visit)]
    [InlineData(-5, ChannelActivityKinds.Overlay)]
    public async Task ActivityConsumer_IgnoresInvalidEvents(int twitchUserId, string kind)
    {
        var repository = new RecordingRepository();
        var consumer = new ChannelActivityObservedConsumer(repository, NullLogger<ChannelActivityObservedConsumer>.Instance);

        await consumer.Consume(Context(new ChannelActivityObservedContract { TwitchUserId = twitchUserId, Kind = kind, OccurredAt = DateTime.UtcNow }));

        Assert.Empty(repository.Recorded);
    }

    [Fact]
    public async Task ActivityConsumer_RecordsKnownKindInUtc()
    {
        var repository = new RecordingRepository();
        var consumer = new ChannelActivityObservedConsumer(repository, NullLogger<ChannelActivityObservedConsumer>.Instance);
        var occurredAt = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

        await consumer.Consume(Context(new ChannelActivityObservedContract { TwitchUserId = 123, Kind = ChannelActivityKinds.Panel, OccurredAt = occurredAt }));

        var recorded = Assert.Single(repository.Recorded);
        Assert.Equal((123, ChannelActivityKinds.Panel, occurredAt), recorded);
    }

    private static ConsumeContext<ChannelActivityObservedContract> Context(ChannelActivityObservedContract message)
    {
        var context = System.Reflection.DispatchProxy.Create<ConsumeContext<ChannelActivityObservedContract>, ContextProxy>();
        ((ContextProxy)(object)context).Message = message;
        return context;
    }

    public class ContextProxy : System.Reflection.DispatchProxy
    {
        public object? Message { get; set; }

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            "get_Message" => Message,
            "get_CancellationToken" => CancellationToken.None,
            _ => throw new NotSupportedException(targetMethod?.Name)
        };
    }

    private sealed class RecordingRepository : IChannelActivityRepository
    {
        public List<(int TwitchUserId, string Kind, DateTime OccurredAt)> Recorded { get; } = [];

        public Task RecordAsync(int twitchUserId, string kind, DateTime occurredAt, CancellationToken cancellationToken = default)
        {
            Recorded.Add((twitchUserId, kind, occurredAt));
            return Task.CompletedTask;
        }

        public Task<ChannelCollectionSnapshot> GetCollectionSnapshotAsync(int twitchUserId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProductData> GetProductDataAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static TwitchTrackingDbContext CreateDb() => new(new DbContextOptionsBuilder<TwitchTrackingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static StreamSession Session(DateTime startedAt, bool live = false, int twitchUserId = 123) => new()
    {
        TwitchUserId = twitchUserId,
        StreamerLogin = "streamer",
        StreamerDisplayName = "Streamer",
        StartedAt = startedAt,
        EndedAt = live ? null : startedAt.AddHours(2),
        IsLive = live
    };


    private static ChannelActivityDay Activity(int twitchUserId, string kind, DateTime seenAt) => new()
    {
        TwitchUserId = twitchUserId,
        Day = DateOnly.FromDateTime(seenAt),
        Kind = kind,
        FirstSeenAt = seenAt,
        LastSeenAt = seenAt
    };
}
