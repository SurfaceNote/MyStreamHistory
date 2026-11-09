using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Gateway.Application.Queries;
using MyStreamHistory.Gateway.Application.Services;
using PlaythroughStreamCategoryDto = MyStreamHistory.Shared.Base.Contracts.Playthroughs.PlaythroughStreamCategoryDto;
using Xunit;
using PlaythroughDto = MyStreamHistory.Shared.Base.Contracts.Playthroughs.PlaythroughDto;

namespace MyStreamHistory.Gateway.Application.Tests;

public class StreamRecapCalculatorTests
{
    private static readonly DateTime Start = new(2026, 11, 8, 18, 0, 0, DateTimeKind.Utc);
    private static readonly RecapStream Current = new(Guid.NewGuid(), Start, Start.AddHours(3), false);

    // Earlier streams, newest first: one per day before the current stream, each two hours long.
    private static readonly List<RecapStream> Earlier = Enumerable.Range(1, 12)
        .Select(day => new RecapStream(Guid.NewGuid(), Start.AddDays(-day), Start.AddDays(-day).AddHours(2), false))
        .ToList();

    [Fact]
    public void FirstTimeReturningAndRegularAccounts_AreToldApart()
    {
        var audience = new List<RecapAudienceMember>
        {
            Member("new", minutes: 40),
            Member("regular", minutes: 170),
            Member("back", minutes: 90),
            Member("recent", minutes: 60),
        };
        var appearances = new Dictionary<string, HashSet<Guid>>
        {
            ["regular"] = [Earlier[0].Id, Earlier[1].Id, Earlier[2].Id, Earlier[3].Id, Earlier[5].Id],
            ["back"] = [Earlier[4].Id, Earlier[9].Id],
            ["recent"] = [Earlier[1].Id],
        };

        var recap = Build(audience, appearances);

        Assert.Equal(1, recap.NewCount);
        Assert.Equal("new", Assert.Single(recap.NewViewers).TwitchUserId);
        var returning = Assert.Single(recap.ReturningViewers);
        Assert.Equal("back", returning.TwitchUserId);
        Assert.Equal(4, returning.MissedStreams);
        var regular = Assert.Single(recap.StreakViewers);
        Assert.Equal("regular", regular.TwitchUserId);
        Assert.Equal(5, regular.Streak);
        Assert.Equal(4, recap.ChatAccounts);
    }

    [Fact]
    public void Averages_UseTheTenLatestFinishedStreams()
    {
        var counts = Earlier.Take(10).ToDictionary(s => s.Id, _ => 6);
        counts[Earlier[11].Id] = 100;

        var recap = Build([Member("a", 10)], new(), counts, audienceAverages: [4m, null, 9.4m, 7m]);

        Assert.Equal(180, recap.DurationMinutes);
        Assert.Equal(120, recap.AverageDurationMinutes);
        Assert.Equal(6, recap.AverageChatAccounts);
        Assert.Equal(10, recap.ComparisonStreamsCount);
        Assert.Equal(12, recap.PreviousStreamsCount);
        Assert.Equal(9, recap.PeakViewers);
    }

    [Fact]
    public void FirstRecordedStream_HasNoNewcomersOrComparison()
    {
        var recap = StreamRecapCalculator.Build(Current, [], [Member("a", 10)], [], new Dictionary<string, HashSet<Guid>>(),
            new Dictionary<Guid, int>(), [], Start.AddHours(4));

        Assert.Equal(0, recap.PreviousStreamsCount);
        Assert.Equal(0, recap.NewCount);
        Assert.Null(recap.AverageDurationMinutes);
        Assert.Null(recap.AverageChatAccounts);
    }

    [Fact]
    public void StreamsAfterTheCurrentOne_AreIgnored()
    {
        var later = new RecapStream(Guid.NewGuid(), Start.AddDays(1), Start.AddDays(1).AddHours(5), false);
        var appearances = new Dictionary<string, HashSet<Guid>> { ["a"] = [later.Id] };

        var recap = StreamRecapCalculator.Build(Current, [], [Member("a", 10)], [later, Earlier[0]], appearances,
            new Dictionary<Guid, int>(), [], Start.AddHours(4));

        Assert.Equal(1, recap.PreviousStreamsCount);
        Assert.Equal(1, recap.NewCount);
    }

    [Fact]
    public void PlaythroughShowsTheStreamPosition()
    {
        var playthrough = new PlaythroughDto
        {
            Id = Guid.NewGuid(), GameName = "Mortal Shell II", Status = "Playing",
            StreamCategories =
            [
                Segment(Earlier[1]), Segment(Earlier[0]), Segment(Current), Segment(Current)
            ]
        };

        var recap = StreamRecapCalculator.Build(Current, [], null, Earlier, new Dictionary<string, HashSet<Guid>>(),
            new Dictionary<Guid, int>(), [playthrough], Start.AddHours(4));

        var item = Assert.Single(recap.Playthroughs);
        Assert.Equal(3, item.StreamNumber);
        Assert.Equal(3, item.StreamsCount);
        Assert.False(recap.ViewerDataAvailable);
    }

    [Fact]
    public void ReplaysOfAGame_AreNumberedInStartOrder()
    {
        var game = Guid.NewGuid();
        var second = new PlaythroughStatisticsDto { PlaythroughId = Guid.NewGuid(), TwitchCategoryId = game, FirstStreamStartedAt = Start };
        var first = new PlaythroughStatisticsDto { PlaythroughId = Guid.NewGuid(), TwitchCategoryId = game, FirstStreamStartedAt = Start.AddYears(-1) };
        var other = new PlaythroughStatisticsDto { PlaythroughId = Guid.NewGuid(), TwitchCategoryId = Guid.NewGuid() };

        GetStreamerStatisticsQueryHandler.NumberRuns([second, first, other], new Dictionary<Guid, DateTime>());

        Assert.Equal((1, 2), (first.RunNumber, first.RunsCount));
        Assert.Equal((2, 2), (second.RunNumber, second.RunsCount));
        Assert.Equal((1, 1), (other.RunNumber, other.RunsCount));
    }

    private static StreamRecapDto Build(
        List<RecapAudienceMember> audience,
        Dictionary<string, HashSet<Guid>> appearances,
        Dictionary<Guid, int>? counts = null,
        List<decimal?>? audienceAverages = null) =>
        StreamRecapCalculator.Build(Current, audienceAverages ?? [], audience, Earlier, appearances,
            counts ?? new Dictionary<Guid, int>(), [], Start.AddHours(4));

    private static RecapAudienceMember Member(string id, int minutes) => new(id, id, id, null, minutes, 0);

    private static PlaythroughStreamCategoryDto Segment(RecapStream stream) => new()
    {
        StreamCategoryId = Guid.NewGuid(), StreamSessionId = stream.Id, StreamStartedAt = stream.StartedAt,
        CategoryStartedAt = stream.StartedAt
    };
}
