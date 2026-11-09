using MyStreamHistory.TwitchTrackingService.Application.Services;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Application.Tests.Services;

public class PlaythroughRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TwitchCategory Game = new() { TwitchId = "1234", Name = "Mortal Shell II", BoxArtUrl = "" };

    [Fact]
    public void CompletedPlaythrough_StopsCollectingNewStreams()
    {
        Assert.False(PlaythroughRules.CollectsNewStreams(PlaythroughStatus.Completed, autoAddNewStreams: true));
        Assert.True(PlaythroughRules.CollectsNewStreams(PlaythroughStatus.Playing, autoAddNewStreams: true));
        Assert.True(PlaythroughRules.CollectsNewStreams(PlaythroughStatus.Planned, autoAddNewStreams: true));
        Assert.False(PlaythroughRules.CollectsNewStreams(PlaythroughStatus.Playing, autoAddNewStreams: false));
    }

    [Fact]
    public void GameStreamedOnTwoStreams_IsSuggested()
    {
        var segments = new[] { Segment(Now.AddDays(-3)), Segment(Now.AddDays(-1)) };
        Assert.True(PlaythroughRules.ShouldSuggest(Game, segments, [], Now));
    }

    [Fact]
    public void TwoSegmentsOfOneStream_AreNotEnough()
    {
        var stream = Guid.NewGuid();
        var segments = new[] { Segment(Now.AddHours(-3), stream), Segment(Now.AddHours(-1), stream) };
        Assert.False(PlaythroughRules.ShouldSuggest(Game, segments, [], Now));
    }

    [Theory]
    [InlineData("509658", "Just Chatting")]
    [InlineData("26936", "Music")]
    [InlineData("999999", "Just Chatting")]
    public void NonGameCategories_AreNotSuggested(string twitchId, string name)
    {
        var category = new TwitchCategory { TwitchId = twitchId, Name = name, BoxArtUrl = "" };
        var segments = new[] { Segment(Now.AddDays(-3)), Segment(Now.AddDays(-1)) };
        Assert.False(PlaythroughRules.ShouldSuggest(category, segments, [], Now));
    }

    [Fact]
    public void GamesNotStreamedForMoreThanAYear_AreNotSuggested()
    {
        var segments = new[] { Segment(Now.AddDays(-400)), Segment(Now.AddDays(-370)) };
        Assert.False(PlaythroughRules.ShouldSuggest(Game, segments, [], Now));
    }

    [Fact]
    public void GamesStreamedWithinTheLastYear_AreSuggested()
    {
        var segments = new[] { Segment(Now.AddDays(-300)), Segment(Now.AddDays(-200)) };
        Assert.True(PlaythroughRules.ShouldSuggest(Game, segments, [], Now));
    }

    [Theory]
    [InlineData(PlaythroughStatus.Playing, false)]
    [InlineData(PlaythroughStatus.Planned, false)]
    [InlineData(PlaythroughStatus.Dropped, true)]
    public void GameAlreadyOnTheShelf_IsNotSuggested(PlaythroughStatus status, bool autoAdd)
    {
        var playthrough = new Playthrough { Status = status, AutoAddNewStreams = autoAdd, TwitchCategory = Game };
        var segments = new[] { Segment(Now.AddDays(-3)), Segment(Now.AddDays(-1)) };
        Assert.False(PlaythroughRules.ShouldSuggest(Game, segments, [playthrough], Now));
    }

    [Fact]
    public void AfterACompletedPlaythrough_OnlyLaterStreamsCount()
    {
        var finished = new Playthrough { Status = PlaythroughStatus.Completed, AutoAddNewStreams = true, TwitchCategory = Game };
        finished.PlaythroughStreamCategories.Add(new PlaythroughStreamCategory { StreamCategory = Segment(Now.AddDays(-10)) });

        var oneNewStream = new[] { Segment(Now.AddDays(-20)), Segment(Now.AddDays(-2)) };
        Assert.False(PlaythroughRules.ShouldSuggest(Game, oneNewStream, [finished], Now));

        var twoNewStreams = new[] { Segment(Now.AddDays(-5)), Segment(Now.AddDays(-2)) };
        Assert.True(PlaythroughRules.ShouldSuggest(Game, twoNewStreams, [finished], Now));
    }

    private static StreamCategory Segment(DateTime startedAt, Guid? streamId = null) => new()
    {
        StreamSessionId = streamId ?? Guid.NewGuid(),
        StartedAt = startedAt,
        TwitchCategory = Game
    };
}
