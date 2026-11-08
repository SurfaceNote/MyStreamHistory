using Microsoft.EntityFrameworkCore;
using MyStreamHistory.ViewerService.Application.Services;
using MyStreamHistory.ViewerService.Domain.Entities;
using MyStreamHistory.ViewerService.Infrastructure.Persistence;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Api.Tests;

public class TopViewersRankingTests
{
    [Fact]
    public async Task TopViewers_ExcludeBroadcasterAndListedBots_KeepAudienceOrder()
    {
        await using var db = CreateDb();
        Exclude(db, "streamelements", "nightbot");
        AddStats(db, "123", "streamer", 500);
        AddStats(db, "1", "StreamElements", 400);
        AddStats(db, "2", "nightbot", 300);
        AddStats(db, "3", "loyal_viewer", 200);
        AddStats(db, "4", "new_viewer", 10);
        AddStats(db, "5", "other_channel_viewer", 900, streamer: "999");
        await db.SaveChangesAsync();

        var top = await new ViewerStatsRepository(db).GetTopViewersByStreamerAsync("123", 10);

        Assert.Equal(["loyal_viewer", "new_viewer"], top.Select(s => s.Viewer.Login));
    }

    [Fact]
    public async Task RemovingABotFromTheList_ReturnsItToTheRanking()
    {
        await using var db = CreateDb();
        Exclude(db, "nightbot");
        AddStats(db, "2", "nightbot", 300);
        AddStats(db, "3", "loyal_viewer", 200);
        await db.SaveChangesAsync();
        var repository = new RankingExclusionRepository(db);

        Assert.True(await repository.RemoveAsync("nightbot"));
        Assert.False(await repository.RemoveAsync("nightbot"));
        var top = await new ViewerStatsRepository(db).GetTopViewersByStreamerAsync("123", 10);

        Assert.Equal(["nightbot", "loyal_viewer"], top.Select(s => s.Viewer.Login));
    }

    [Fact]
    public async Task AddingABot_IsIdempotentAndRecordsTheActor()
    {
        await using var db = CreateDb();
        var repository = new RankingExclusionRepository(db);

        Assert.True(await repository.AddAsync("kination_bot", "operator"));
        Assert.False(await repository.AddAsync("kination_bot", "someone_else"));

        var account = Assert.Single(await repository.ListAsync());
        Assert.Equal("operator", account.AddedBy);
        Assert.Contains("kination_bot", await repository.GetLoginsAsync());
    }

    [Fact]
    public async Task Candidates_AreAlwaysOnAccounts_NotLoyalViewersOrListedBotsOrTheBroadcaster()
    {
        await using var db = CreateDb();
        Exclude(db, "nightbot");
        // Channel 123: 1000 observed minutes (the broadcaster).
        AddStats(db, "123", "streamer", 0, minutes: 1000);
        AddStats(db, "10", "channel_helper", 0, minutes: 960, messagePoints: 40);
        AddStats(db, "11", "loyal_viewer", 0, minutes: 700, messagePoints: 12);
        AddStats(db, "2", "nightbot", 0, minutes: 1000);
        // A lurker present for most of three channels.
        AddStats(db, "20", "lurker", 0, minutes: 700);
        AddStats(db, "456", "streamer_two", 0, minutes: 800, streamer: "456");
        AddStats(db, "20", "lurker", 0, minutes: 560, streamer: "456");
        AddStats(db, "789", "streamer_three", 0, minutes: 900, streamer: "789");
        AddStats(db, "20", "lurker", 0, minutes: 600, streamer: "789");
        // The broadcaster of 456 watches 123 for the whole stream: a candidate on 123 only.
        AddStats(db, "456", "streamer_two", 0, minutes: 950);
        // A tiny channel is ignored: too little observed time.
        AddStats(db, "999", "tiny", 0, minutes: 100, streamer: "999");
        AddStats(db, "30", "tiny_fan", 0, minutes: 100, streamer: "999");
        await db.SaveChangesAsync();

        var rows = await new RankingExclusionRepository(db).GetHighPresenceRowsAsync(
            BotCandidateRules.MinChannelObservedMinutes, BotCandidateRules.MinRowShare);
        var candidates = BotCandidateRules.Find(rows);

        Assert.Equal(["lurker", "channel_helper", "streamer_two"], candidates.Select(c => c.Login));
        var lurker = candidates[0];
        Assert.Equal(3, lurker.HighPresenceChannels);
        Assert.Equal(3, lurker.ChannelsCount);
        Assert.Equal(1860, lurker.TotalMinutes);
        Assert.Equal(0.96, candidates[1].MaxPresenceShare);
    }

    private static ViewerServiceDbContext CreateDb() => new(new DbContextOptionsBuilder<ViewerServiceDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void Exclude(ViewerServiceDbContext db, params string[] logins)
    {
        foreach (var login in logins)
            db.RankingExcludedAccounts.Add(new RankingExcludedAccount { Login = login, AddedBy = "default", AddedAt = DateTime.UtcNow });
    }

    private static void AddStats(ViewerServiceDbContext db, string twitchUserId, string login, decimal experience,
        string streamer = "123", int minutes = 0, decimal messagePoints = 0)
    {
        var viewer = db.Viewers.Local.FirstOrDefault(v => v.TwitchUserId == twitchUserId);
        if (viewer == null)
        {
            viewer = new Viewer
            {
                Id = Guid.NewGuid(),
                TwitchUserId = twitchUserId,
                Login = login,
                DisplayName = login,
                CreatedAt = DateTime.UtcNow
            };
            db.Add(viewer);
        }
        db.Add(new ViewerStats
        {
            Id = Guid.NewGuid(),
            ViewerId = viewer.Id,
            Viewer = viewer,
            StreamerTwitchUserId = streamer,
            Experience = experience,
            MinutesWatched = minutes,
            EarnedMsgPoints = messagePoints,
            LastUpdatedAt = DateTime.UtcNow
        });
    }
}
