using Microsoft.EntityFrameworkCore;
using MyStreamHistory.TwitchTrackingService.Application.Services;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Repositories;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Api.Tests;

public class GameStatsRepositoryTests
{
    private static readonly DateTime At = new(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GamePageReadsSegmentsLanguageAndPlaythroughs()
    {
        await using var db = CreateDb();
        var game = new TwitchCategory { TwitchId = "1234567", Name = "Silksong", BoxArtUrl = "art", IgdbId = "12345" };
        var other = new TwitchCategory { TwitchId = "999", Name = "Other", BoxArtUrl = "art", IgdbId = "1" };
        var first = Session(1, "ru", At.AddDays(-2));
        var second = Session(1, "ru", At.AddDays(-1));
        var gameOnFirst = new StreamCategory { StreamSession = first, TwitchCategory = game, StartedAt = first.StartedAt, EndedAt = first.StartedAt.AddHours(2) };
        var gameOnSecond = new StreamCategory { StreamSession = second, TwitchCategory = game, StartedAt = second.StartedAt, EndedAt = second.StartedAt.AddHours(1) };
        var otherOnSecond = new StreamCategory { StreamSession = second, TwitchCategory = other, StartedAt = second.StartedAt.AddHours(1), EndedAt = second.StartedAt.AddHours(3) };
        db.StreamCategories.AddRange(gameOnFirst, gameOnSecond, otherOnSecond);
        db.Playthroughs.Add(new Playthrough
        {
            TwitchUserId = 1,
            TwitchCategory = game,
            Title = "Silksong",
            Status = PlaythroughStatus.Completed,
            PlaythroughStreamCategories =
            [
                new() { StreamCategory = gameOnFirst },
                new() { StreamCategory = gameOnSecond }
            ]
        });
        await db.SaveChangesAsync();

        var stats = await new GameStatsService(new GameStatsRepository(db)).GetGameAsync("1234567");

        Assert.NotNull(stats);
        Assert.Equal(3, stats.Summary.TotalHours, 3);
        Assert.Equal(1, stats.Summary.CompletedCount);
        var streamer = Assert.Single(stats.Streamers);
        Assert.Equal("ru", streamer.Language);
        Assert.Equal(2, streamer.StreamsCount);
        Assert.Equal(2, streamer.Playthrough!.StreamsCount);
    }

    [Fact]
    public async Task CatalogReadsOnlyGames()
    {
        await using var db = CreateDb();
        var session = Session(1, "en", At);
        db.StreamCategories.AddRange(
            new StreamCategory
            {
                StreamSession = session,
                TwitchCategory = new TwitchCategory { TwitchId = "1", Name = "Game", BoxArtUrl = "", IgdbId = "7" },
                StartedAt = At,
                EndedAt = At.AddHours(1)
            },
            new StreamCategory
            {
                StreamSession = session,
                TwitchCategory = new TwitchCategory { TwitchId = "509658", Name = "Just Chatting", BoxArtUrl = "" },
                StartedAt = At.AddHours(1),
                EndedAt = At.AddHours(2)
            });
        await db.SaveChangesAsync();

        var catalog = await new GameStatsService(new GameStatsRepository(db)).GetCatalogAsync();

        Assert.Equal(new[] { "Game" }, catalog.Select(g => g.Name));
    }

    private static TwitchTrackingDbContext CreateDb() => new(new DbContextOptionsBuilder<TwitchTrackingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static StreamSession Session(int twitchUserId, string language, DateTime startedAt) => new()
    {
        TwitchUserId = twitchUserId,
        StreamerLogin = $"streamer{twitchUserId}",
        StreamerDisplayName = $"Streamer{twitchUserId}",
        StartedAt = startedAt,
        EndedAt = startedAt.AddHours(3),
        Language = language
    };
}
