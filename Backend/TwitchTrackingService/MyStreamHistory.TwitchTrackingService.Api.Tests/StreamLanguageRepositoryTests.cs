using Microsoft.EntityFrameworkCore;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Repositories;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Api.Tests;

public class StreamLanguageRepositoryTests
{
    private static readonly DateTime At = new(2026, 11, 20, 18, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task BackfillCandidatesAreEndedStreamsWithoutLanguage()
    {
        await using var db = CreateDb();
        db.StreamSessions.AddRange(
            Session(1, language: null, live: false),
            Session(1, language: null, live: false),
            Session(2, language: "ru", live: false),
            // A live stream gets its language from polling, not from the backfill.
            Session(3, language: null, live: true));
        await db.SaveChangesAsync();

        var userIds = await new StreamSessionRepository(db).GetTwitchUserIdsWithoutLanguageAsync();

        Assert.Equal(new[] { 1 }, userIds);
    }

    [Fact]
    public async Task StreamDtosExposeLanguage()
    {
        await using var db = CreateDb();
        var session = Session(1, language: "cs", live: false);
        db.StreamSessions.Add(session);
        await db.SaveChangesAsync();
        var repository = new StreamSessionRepository(db);

        var byId = await repository.GetDtoByIdAsync(session.Id);
        var recent = await repository.GetRecentStreamsByTwitchUserIdAsync(1);

        Assert.Equal("cs", byId!.Language);
        Assert.Equal("cs", Assert.Single(recent).Language);
    }

    private static TwitchTrackingDbContext CreateDb() => new(new DbContextOptionsBuilder<TwitchTrackingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static StreamSession Session(int twitchUserId, string? language, bool live) => new()
    {
        TwitchUserId = twitchUserId,
        StreamerLogin = $"streamer{twitchUserId}",
        StreamerDisplayName = $"Streamer{twitchUserId}",
        StartedAt = At,
        EndedAt = live ? null : At.AddHours(2),
        IsLive = live,
        Language = language
    };
}
