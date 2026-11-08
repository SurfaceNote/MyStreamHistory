using Microsoft.EntityFrameworkCore;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions;
using MyStreamHistory.TwitchTrackingService.Api.Consumers;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Repositories;
using MyStreamHistory.ViewerService.Api.Consumers;
using MyStreamHistory.ViewerService.Domain.Entities;
using MyStreamHistory.ViewerService.Infrastructure.Persistence;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Api.Tests;

public class AdminStreamsTests
{
    [Fact]
    public async Task PagesAreOrderedStableAndClampedAndIncludeAllCategories()
    {
        await using var db = new TwitchTrackingDbContext(new DbContextOptionsBuilder<TwitchTrackingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var at = new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);
        var game = new TwitchCategory { Name = "Game", TwitchId = "1", BoxArtUrl = "" };
        var latest = Session(at);
        latest.EndedAt = at.AddHours(2.5);
        latest.StreamCategories = [new() { TwitchCategory = game, StartedAt = at },
            new() { TwitchCategory = game, StartedAt = at.AddHours(1) }];
        db.StreamSessions.AddRange(latest, Session(at.AddDays(-1)), Session(at.AddDays(-2)));
        await db.SaveChangesAsync();
        var repository = new StreamSessionRepository(db);
        var first = await GetAdminStreamsConsumer.ReadPageAsync(repository, new() { PageSize = 2 });
        var last = await GetAdminStreamsConsumer.ReadPageAsync(repository, new() { PageSize = 2, Page = int.MaxValue });
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(latest.Id, first.Items[0].Id);
        Assert.Equal(2.5, first.Items[0].DurationHours);
        Assert.Equal(new[] { "Game" }, first.Items[0].Categories);
        Assert.Equal(2, first.Items[0].CategoryIds.Count);
        Assert.Equal(2, last.Page);
        Assert.Single(last.Items);
        Assert.DoesNotContain(last.Items[0].Id, first.Items.Select(s => s.Id));
    }

    [Fact]
    public async Task EmptyPageAndLiveOrMissingEndTimesAreHandled()
    {
        await using var db = new TwitchTrackingDbContext(new DbContextOptionsBuilder<TwitchTrackingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var repository = new StreamSessionRepository(db);
        var empty = await GetAdminStreamsConsumer.ReadPageAsync(repository, new() { Page = 100 });
        Assert.Equal(1, empty.Page);
        Assert.Empty(empty.Items);
        var live = Session(DateTime.UtcNow.AddHours(-1)); live.IsLive = true;
        var unknownEnd = Session(DateTime.UtcNow.AddDays(-1));
        db.StreamSessions.AddRange(live, unknownEnd);
        await db.SaveChangesAsync();
        var page = await GetAdminStreamsConsumer.ReadPageAsync(repository, new());
        Assert.InRange(page.Items.Single(s => s.Id == live.Id).DurationHours!.Value, 1, 1.1);
        Assert.Null(page.Items.Single(s => s.Id == unknownEnd.Id).DurationHours);
    }

    [Fact]
    public async Task ViewersAreDeduplicatedAcrossCategoriesAndPresenceAndIsolatedByStream()
    {
        await using var db = new ViewerServiceDbContext(new DbContextOptionsBuilder<ViewerServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var stream = Guid.NewGuid(); var noCategory = Guid.NewGuid(); var empty = Guid.NewGuid();
        var firstCategory = Guid.NewGuid(); var secondCategory = Guid.NewGuid();
        var viewer = new Viewer { Id = Guid.NewGuid(), TwitchUserId = "1" };
        db.ViewerCategoryWatches.AddRange(
            new() { Id = Guid.NewGuid(), Viewer = viewer, StreamCategoryId = firstCategory },
            new() { Id = Guid.NewGuid(), Viewer = viewer, StreamCategoryId = secondCategory });
        var minute = DateTime.UtcNow;
        db.CommunityPresences.AddRange(
            new() { StreamerTwitchUserId = "10", StreamSessionId = stream, ViewerTwitchUserId = "1", Minute = minute },
            new() { StreamerTwitchUserId = "10", StreamSessionId = stream, ViewerTwitchUserId = "2", Minute = minute },
            new() { StreamerTwitchUserId = "10", StreamSessionId = stream, ViewerTwitchUserId = "2", Minute = minute.AddMinutes(1) },
            new() { StreamerTwitchUserId = "10", StreamSessionId = noCategory, ViewerTwitchUserId = "3", Minute = minute },
            new() { StreamerTwitchUserId = "10", StreamSessionId = Guid.NewGuid(), ViewerTwitchUserId = "4", Minute = minute });
        await db.SaveChangesAsync();
        var result = await GetAdminStreamViewerCountsConsumer.ReadCountsAsync(db, new() { Streams = [
            new() { StreamSessionId = stream, CategoryIds = [firstCategory, secondCategory] },
            new() { StreamSessionId = noCategory }, new() { StreamSessionId = empty } ] });
        Assert.Equal(2, result.Counts[stream]);
        Assert.Equal(1, result.Counts[noCategory]);
        Assert.Equal(0, result.Counts[empty]);
    }

    private static StreamSession Session(DateTime at) => new()
    {
        TwitchUserId = 1, StreamerLogin = "streamer", StreamerDisplayName = "Streamer", StartedAt = at
    };
}
