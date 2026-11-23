using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Application.Services;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Application.Tests.Services;

public class GameStatsCalculatorTests
{
    private static readonly DateTime Now = new(2026, 11, 23, 20, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Silksong = Guid.NewGuid();
    private static readonly Guid JustChatting = Guid.NewGuid();

    [Fact]
    public void LeavingAndReturningToAGameInOneStreamCountsOneStreamAndSumsHours()
    {
        var stream = Guid.NewGuid();
        var game = Single(Build(
            Segment(1, stream, Now.AddDays(-2), hours: 1),
            Segment(1, stream, Now.AddDays(-2).AddHours(2), hours: 1.5)));

        Assert.Equal(2.5, game.Summary.TotalHours, 3);
        Assert.Equal(1, game.Summary.StreamsCount);
        var streamer = Assert.Single(game.Streamers);
        Assert.Equal(1, streamer.StreamsCount);
        Assert.Equal(2.5, streamer.Hours, 3);
    }

    [Fact]
    public void SummaryCountsChannelsStreamsAndDates()
    {
        var first = Now.AddDays(-40);
        var last = Now.AddDays(-1);
        var game = Single(Build(
            Segment(1, Guid.NewGuid(), first, hours: 2),
            Segment(1, Guid.NewGuid(), Now.AddDays(-10), hours: 3),
            Segment(2, Guid.NewGuid(), last, hours: 1)));

        Assert.Equal(6, game.Summary.TotalHours, 3);
        Assert.Equal(3, game.Summary.StreamsCount);
        Assert.Equal(2, game.Summary.ChannelsCount);
        Assert.Equal(first, game.Summary.FirstStreamedAt);
        Assert.Equal(last, game.Summary.LastStreamedAt);
    }

    [Fact]
    public void RecentHoursOnlyCountTheLast30Days()
    {
        var game = Single(Build(
            Segment(1, Guid.NewGuid(), Now.AddDays(-45), hours: 4),
            // Starts one hour before the period: only the last hour is recent.
            Segment(1, Guid.NewGuid(), Now.AddDays(-30).AddHours(-1), hours: 2),
            Segment(1, Guid.NewGuid(), Now.AddDays(-3), hours: 3)));

        Assert.Equal(9, game.Summary.TotalHours, 3);
        Assert.Equal(4, game.Summary.HoursLast30Days, 3);
    }

    [Fact]
    public void OpenSegmentOfALiveStreamRunsUntilNowAndMarksTheStreamerLive()
    {
        var game = Single(Build(
            Segment(1, Guid.NewGuid(), Now.AddHours(-1.5), hours: null, sessionLive: true),
            Segment(2, Guid.NewGuid(), Now.AddDays(-1), hours: 2)));

        var live = game.Streamers[0];
        Assert.Equal(1, live.TwitchUserId);
        Assert.True(live.IsLiveWithGame);
        Assert.Equal(1.5, live.Hours, 3);
        Assert.False(game.Streamers[1].IsLiveWithGame);
        Assert.Equal(1, game.Summary.LiveCount);
    }

    [Fact]
    public void OpenSegmentOfAStreamStuckAsLiveEndsAtTheLastLiveConfirmation()
    {
        var start = Now.AddDays(-50);
        var segment = Segment(1, Guid.NewGuid(), start, hours: null, sessionLive: true) with
        {
            SessionLastConfirmedLiveAt = start.AddHours(3)
        };

        var game = Single(Build(segment));

        Assert.Equal(3, game.Summary.TotalHours, 3);
        Assert.Equal(0, game.Summary.HoursLast30Days, 3);
        Assert.False(game.Streamers[0].IsLiveWithGame);
    }

    [Fact]
    public void RecentlyConfirmedLiveStreamIsLive()
    {
        var segment = Segment(1, Guid.NewGuid(), Now.AddHours(-2), hours: null, sessionLive: true) with
        {
            SessionLastConfirmedLiveAt = Now.AddMinutes(-5)
        };

        var streamer = Single(Build(segment)).Streamers[0];

        Assert.True(streamer.IsLiveWithGame);
        Assert.Equal(2 - 5 / 60.0, streamer.Hours, 3);
    }

    [Fact]
    public void LiveStreamThatSwitchedToAnotherGameIsNotLiveWithThisOne()
    {
        var game = Single(Build(Segment(1, Guid.NewGuid(), Now.AddHours(-3), hours: 1, sessionLive: true)));

        Assert.False(game.Streamers[0].IsLiveWithGame);
        Assert.Equal(0, game.Summary.LiveCount);
    }

    [Fact]
    public void OpenSegmentOfAnEndedStreamEndsWithTheStream()
    {
        var start = Now.AddDays(-1);
        var segment = Segment(1, Guid.NewGuid(), start, hours: null) with { SessionEndedAt = start.AddHours(2) };

        Assert.Equal(2, Single(Build(segment)).Summary.TotalHours, 3);
    }

    [Fact]
    public void StreamerLanguageIsTheMostCommonAndTiesGoToTheLatest()
    {
        var game = Single(Build(
            Segment(1, Guid.NewGuid(), Now.AddDays(-5), hours: 1, language: "ru"),
            Segment(1, Guid.NewGuid(), Now.AddDays(-4), hours: 1, language: "ru"),
            Segment(1, Guid.NewGuid(), Now.AddDays(-3), hours: 1, language: "en"),
            Segment(2, Guid.NewGuid(), Now.AddDays(-5), hours: 1, language: "de"),
            Segment(2, Guid.NewGuid(), Now.AddDays(-2), hours: 1, language: "ru"),
            Segment(3, Guid.NewGuid(), Now.AddDays(-2), hours: 1, language: null)));

        var byUser = game.Streamers.ToDictionary(s => s.TwitchUserId);
        Assert.Equal("ru", byUser[1].Language);
        Assert.Equal("ru", byUser[2].Language);
        Assert.Null(byUser[3].Language);
        Assert.Equal(new[] { "ru" }, game.Summary.Languages);
    }

    [Fact]
    public void GameLanguagesAreOrderedByNumberOfStreamers()
    {
        var game = Single(Build(
            Segment(1, Guid.NewGuid(), Now.AddDays(-1), hours: 1, language: "en"),
            Segment(2, Guid.NewGuid(), Now.AddDays(-1), hours: 1, language: "cs"),
            Segment(3, Guid.NewGuid(), Now.AddDays(-1), hours: 1, language: "cs")));

        Assert.Equal(new[] { "cs", "en" }, game.Summary.Languages);
    }

    [Fact]
    public void StreamerShowsTheLatestPlaythroughAndCompletedCountsAnyCompletion()
    {
        var game = Single(GameStatsCalculator.Build(
            [
                Segment(1, Guid.NewGuid(), Now.AddDays(-3), hours: 1),
                Segment(2, Guid.NewGuid(), Now.AddDays(-2), hours: 1),
                Segment(3, Guid.NewGuid(), Now.AddDays(-1), hours: 1)
            ],
            [
                // Streamer 1 finished the game and is now replaying it.
                new GamePlaythroughRow(Silksong, 1, PlaythroughStatus.Completed, "First run", Now.AddDays(-20), 7),
                new GamePlaythroughRow(Silksong, 1, PlaythroughStatus.Playing, "Steel soul", Now.AddDays(-2), 2),
                new GamePlaythroughRow(Silksong, 2, PlaythroughStatus.Dropped, "Silksong", Now.AddDays(-2), 3),
                new GamePlaythroughRow(JustChatting, 3, PlaythroughStatus.Completed, "Other game", Now, 1)
            ],
            Now));

        var byUser = game.Streamers.ToDictionary(s => s.TwitchUserId);
        Assert.Equal(new GamePlaythroughSummary(PlaythroughStatus.Playing, "Steel soul", 2), byUser[1].Playthrough);
        Assert.Equal(PlaythroughStatus.Dropped, byUser[2].Playthrough!.Status);
        Assert.Null(byUser[3].Playthrough);
        Assert.Equal(1, game.Summary.CompletedCount);
    }

    [Fact]
    public void StreamersAreOrderedLiveFirstThenByLastStream()
    {
        var game = Single(Build(
            Segment(1, Guid.NewGuid(), Now.AddDays(-10), hours: 1),
            Segment(2, Guid.NewGuid(), Now.AddDays(-1), hours: 1),
            Segment(3, Guid.NewGuid(), Now.AddDays(-20), hours: null, sessionLive: true) with { StartedAt = Now.AddHours(-1) }));

        Assert.Equal(new[] { 3, 2, 1 }, game.Streamers.Select(s => s.TwitchUserId));
    }

    [Fact]
    public void StreamerNameComesFromTheLatestStream()
    {
        var game = Single(Build(
            Segment(1, Guid.NewGuid(), Now.AddDays(-10), hours: 1) with { StreamerDisplayName = "OldName" },
            Segment(1, Guid.NewGuid(), Now.AddDays(-1), hours: 1) with { StreamerDisplayName = "NewName" }));

        Assert.Equal("NewName", game.Streamers[0].StreamerDisplayName);
    }

    [Fact]
    public async Task CatalogLeavesOutNonGameCategories()
    {
        var repository = Fake<IGameStatsRepository>.Create(new()
        {
            [nameof(IGameStatsRepository.GetSegmentsAsync)] = _ => Task.FromResult<IReadOnlyList<GameSegmentRow>>(
            [
                Segment(1, Guid.NewGuid(), Now.AddDays(-1), hours: 1),
                Segment(1, Guid.NewGuid(), Now.AddDays(-1), hours: 1) with
                {
                    TwitchCategoryId = JustChatting, CategoryTwitchId = "509658", Name = "Just Chatting", IgdbId = null
                }
            ]),
            [nameof(IGameStatsRepository.GetPlaythroughsAsync)] = _ => Task.FromResult<IReadOnlyList<GamePlaythroughRow>>([])
        });

        var catalog = await new GameStatsService(repository).GetCatalogAsync();

        Assert.Equal(new[] { "Hollow Knight: Silksong" }, catalog.Select(g => g.Name));
    }

    [Fact]
    public async Task UnknownGameHasNoStats()
    {
        var repository = Fake<IGameStatsRepository>.Create(new()
        {
            [nameof(IGameStatsRepository.GetSegmentsAsync)] = _ => Task.FromResult<IReadOnlyList<GameSegmentRow>>([])
        });

        Assert.Null(await new GameStatsService(repository).GetGameAsync("404"));
    }

    private static IReadOnlyList<GameStats> Build(params GameSegmentRow[] segments) =>
        GameStatsCalculator.Build(segments, [], Now);

    private static GameStats Single(IReadOnlyList<GameStats> games) => Assert.Single(games);

    /// <summary>A Silksong segment; <paramref name="hours"/> null leaves it open.</summary>
    private static GameSegmentRow Segment(
        int userId,
        Guid streamId,
        DateTime startedAt,
        double? hours,
        bool sessionLive = false,
        string? language = "ru") =>
        new(
            Silksong,
            "1234567",
            "Hollow Knight: Silksong",
            "https://box/art.jpg",
            "12345",
            streamId,
            userId,
            $"streamer{userId}",
            $"Streamer{userId}",
            null,
            language,
            sessionLive,
            null,
            startedAt,
            sessionLive ? null : startedAt.AddHours(hours ?? 0),
            startedAt,
            hours == null ? null : startedAt.AddHours(hours.Value));
}
