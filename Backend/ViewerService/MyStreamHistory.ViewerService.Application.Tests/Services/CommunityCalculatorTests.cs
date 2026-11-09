using MyStreamHistory.Shared.Base.Contracts.Community;
using MyStreamHistory.ViewerService.Application.Services;
using MyStreamHistory.ViewerService.Domain.Entities;
using Xunit;
using S = MyStreamHistory.Shared.Base.Contracts.Community.CommunitySegments;

namespace MyStreamHistory.ViewerService.Application.Tests.Services;

public class CommunityCalculatorTests
{
    private const string Channel = "10";
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Builds <paramref name="count"/> daily 90-minute broadcasts ending yesterday, oldest first.</summary>
    private sealed class Channelish(int count, int everyDays = 1)
    {
        public List<CommunitySessionSummary> Sessions { get; } = Enumerable.Range(0, count).Select(i =>
        {
            var start = Now.Date.AddDays(-(count - i) * everyDays).AddHours(18);
            return new CommunitySessionSummary
            {
                StreamerTwitchUserId = Channel, StreamSessionId = Guid.NewGuid(), StartedAt = start, EndedAt = start.AddMinutes(90),
                ExpectedMinutes = 90, CompleteMinutes = 90
            };
        }).ToList();
        public List<CommunitySessionVisit> Visits { get; } = [];
        public List<CommunityMessageAggregate> Messages { get; } = [];
        public HashSet<string> Exclusions { get; } = [];

        public Channelish Attend(string viewer, IEnumerable<int> broadcasts, int minutes = 30, int messages = 0)
        {
            foreach (var i in broadcasts)
            {
                Visits.Add(new() { StreamerTwitchUserId = Channel, StreamSessionId = Sessions[i].StreamSessionId, ViewerTwitchUserId = viewer, Minutes = minutes });
                if (messages > 0)
                    Messages.Add(new() { StreamerTwitchUserId = Channel, StreamSessionId = Sessions[i].StreamSessionId, ViewerTwitchUserId = viewer, Count = messages });
            }
            return this;
        }

        public CommunityCalculation Run(IReadOnlySet<string>? bots = null, string zone = "UTC") => CommunityCalculator.Calculate(new()
        {
            Now = Now, TimeZoneId = zone, StreamerTwitchUserId = Channel, Sessions = Sessions, Visits = Visits, Messages = Messages,
            Exclusions = Exclusions, ExcludedLogins = bots ?? new HashSet<string>(),
            Profiles = Visits.Select(v => v.ViewerTwitchUserId).Concat(Messages.Select(m => m.ViewerTwitchUserId)).Distinct()
                .Select(id => new CommunityProfile(id, "Name " + id, "login" + id, null)).ToList()
        });
    }

    private static IEnumerable<int> Range(int from, int to) => Enumerable.Range(from, to - from + 1);
    private static CommunityMember Member(CommunityCalculation result, string id) => Assert.Single(result.Members, m => m.TwitchUserId == id);

    [Theory]
    [InlineData(90, 10)]
    [InlineData(100, 10)]
    [InlineData(240, 24)]
    [InlineData(301, 31)]
    public void VisitNeedsTenMinutesOrATenthOfTheBroadcast(double minutes, int expected) =>
        Assert.Equal(expected, CommunityCalculator.VisitThreshold(minutes));

    [Fact]
    public void ShortPresenceIsNotAVisitButAMessageIs()
    {
        var data = new Channelish(20).Attend("lurker", [19], minutes: 9).Attend("chatter", [19], minutes: 1, messages: 1);
        var result = data.Run();
        Assert.DoesNotContain(result.Members, m => m.TwitchUserId == "lurker");
        Assert.Equal(S.New, Member(result, "chatter").Segment);
    }

    [Fact]
    public void AttendanceShareAcrossTwentyBroadcastsDefinesCoreRegularAndOccasional()
    {
        var data = new Channelish(20)
            .Attend("core", Range(8, 19))                    // 12 / 20 = 60%
            .Attend("regular", [0, 3, 6, 9, 12, 15])          // 6 / 20 = 30%, last visit 4 broadcasts ago
            .Attend("occasional", [0, 4, 8, 12, 16]);         // 5 / 20 = 25%
        var result = data.Run();
        Assert.Equal("verified", result.Report.Quality);
        Assert.Equal(S.Core, Member(result, "core").Segment);
        Assert.Equal(S.Regular, Member(result, "regular").Segment);
        Assert.Equal(S.Occasional, Member(result, "occasional").Segment);
        Assert.Equal(1, result.Report.Segments[S.Core]);
        Assert.Equal(20, result.Report.WindowDates.Count);
        Assert.Equal("00000000111111111111", Member(result, "core").Attendance);
        Assert.Equal(new[] { "core", "regular", "occasional" }, result.Members.Select(m => m.TwitchUserId));
    }

    [Fact]
    public void CoreFadesAfterThreeMissesAndRegularAfterFive()
    {
        var data = new Channelish(30)
            .Attend("core-2", Range(10, 27))
            .Attend("core-3", Range(10, 26))
            .Attend("regular-4", [10, 13, 16, 19, 22, 25])
            .Attend("regular-5", [9, 12, 15, 18, 21, 24]);
        var result = data.Run();
        Assert.Equal(S.Core, Member(result, "core-2").Segment);
        Assert.Equal(S.Fading, Member(result, "core-3").Segment);
        Assert.Equal(3, Member(result, "core-3").MissedStreak);
        Assert.Equal(S.Regular, Member(result, "regular-4").Segment);
        Assert.Equal(S.Fading, Member(result, "regular-5").Segment);
        Assert.Equal(2, result.Report.Trend[^1].BecameFading);
    }

    [Fact]
    public void TenMissedBroadcastsMeanLostAndNinetyDaysMeanForgotten()
    {
        var data = new Channelish(40)
            .Attend("lost", [25, 27, 29])
            .Attend("one-time", [20])
            .Attend("almost", [28, 30]);
        var result = data.Run();
        Assert.Equal(S.Lost, Member(result, "lost").Segment);
        Assert.Equal(S.OneTime, Member(result, "one-time").Segment);
        Assert.Equal(S.Occasional, Member(result, "almost").Segment);

        var old = new Channelish(30, everyDays: 4).Attend("gone", [0, 1, 2]).Run();
        Assert.Empty(old.Members);
    }

    [Fact]
    public void NewcomersStayNewForThreeBroadcastsAndComebacksAreReported()
    {
        var data = new Channelish(30)
            .Attend("fresh", [27, 29])
            .Attend("older", [26])
            .Attend("back", [4, 6, 8, 29])
            .Attend("short-gap", [15, 17, 19, 29]);
        var result = data.Run();
        Assert.Equal(S.New, Member(result, "fresh").Segment);
        Assert.Equal(S.OneTime, Member(result, "older").Segment);
        Assert.Equal(S.Returned, Member(result, "back").Segment);
        Assert.NotEqual(S.Returned, Member(result, "short-gap").Segment);
        var last = result.Report.LastBroadcast!;
        Assert.Equal(3, last.Visitors);
        Assert.Equal(0, last.NewCount);
        Assert.Equal("back", Assert.Single(last.Returned).TwitchUserId);
        Assert.Equal(1, result.Report.Trend[^1].Returned);
        Assert.Equal(1, result.Report.Trend[^3].NewViewers);
    }

    [Fact]
    public void MissingCoreListsCoreMembersAbsentFromTheLastBroadcast()
    {
        var result = new Channelish(20).Attend("present", Range(0, 19)).Attend("absent", Range(0, 18)).Run();
        Assert.Equal("absent", Assert.Single(result.Report.LastBroadcast!.MissingCore).TwitchUserId);
        Assert.Equal(S.Core, Member(result, "absent").Segment);
    }

    [Fact]
    public void FewBroadcastsAreInsufficientForCoreButStillShowNewcomers()
    {
        var result = new Channelish(4).Attend("always", Range(0, 3)).Attend("fresh", [3]).Run();
        Assert.Equal("insufficient", result.Report.Quality);
        Assert.Equal(S.Occasional, Member(result, "always").Segment);
        Assert.Equal(S.New, Member(result, "fresh").Segment);
        Assert.Equal(1, result.Report.LastBroadcast!.NewCount);

        var partial = new Channelish(8).Attend("always", Range(0, 7)).Run();
        Assert.Equal("provisional", partial.Report.Quality);
        Assert.Equal(S.Core, Member(partial, "always").Segment);
    }

    [Fact]
    public void BroadcastWithIncompleteCollectionIsSkippedRatherThanCountedAsAbsence()
    {
        var data = new Channelish(21).Attend("core", Range(0, 20).Where(i => i != 20));
        data.Sessions[20].CompleteMinutes = 60;
        var result = data.Run();
        Assert.Equal(1, result.Report.SkippedBroadcasts);
        Assert.Equal(20, result.Report.TotalBroadcasts);
        Assert.Equal(S.Core, Member(result, "core").Segment);
        Assert.Equal(0, Member(result, "core").MissedStreak);
    }

    [Fact]
    public void BroadcasterExcludedAccountsAndBotsNeverAppear()
    {
        var data = new Channelish(10).Attend(Channel, Range(0, 9)).Attend("hidden", Range(0, 9)).Attend("bot", Range(0, 9)).Attend("viewer", [9]);
        data.Exclusions.Add("hidden");
        var result = data.Run(new HashSet<string> { "loginbot" });
        Assert.Equal("viewer", Assert.Single(result.Members).TwitchUserId);
    }

    [Fact]
    public void ActivityTagsSeparateChattyAndSilentRegulars()
    {
        var result = new Channelish(20).Attend("talker", Range(10, 19), messages: 3).Attend("quiet", Range(10, 19)).Run();
        Assert.Equal(S.Chatty, Member(result, "talker").Activity);
        Assert.Equal(30, Member(result, "talker").MessagesInWindow);
        Assert.Equal(S.Silent, Member(result, "quiet").Activity);
        Assert.Equal(0, Member(result, "quiet").MessagesInWindow);
        Assert.Equal(1, result.Report.Activity[S.Silent]);
    }

    [Fact]
    public void HistoricalEstimatesAreProvisionalAndUseRecordedChatPoints()
    {
        var data = new Channelish(20);
        foreach (var session in data.Sessions) { session.Legacy = true; session.ExpectedMinutes = session.CompleteMinutes = 0; }
        data.Attend("old", Range(0, 19));
        foreach (var visit in data.Visits) visit.LegacyChat = true;
        var result = data.Run();
        Assert.Equal("provisional", result.Report.Quality);
        Assert.Null(result.Report.Coverage);
        Assert.Equal(S.Core, Member(result, "old").Segment);
        Assert.Equal(S.Chatty, Member(result, "old").Activity);
        Assert.Null(Member(result, "old").MessagesInWindow);
    }

    [Fact]
    public void TrendHasSegmentSizesPerBroadcastAndComparesWithTenBroadcastsAgo()
    {
        var data = new Channelish(70).Attend("steady", Range(0, 69)).Attend("late", Range(55, 69));
        var result = data.Run();
        Assert.Equal(CommunityCalculator.TrendLength, result.Report.Trend.Count);
        Assert.Equal(result.Report.AsOf, result.Report.Trend[^1].Date);
        Assert.Equal(2, result.Report.Trend[^1].Segments[S.Core]);
        Assert.Equal(1, result.Report.Trend[^15].NewViewers);
        Assert.Equal(result.Report.Trend[^11].Date, result.Report.PreviousDate);
        Assert.Equal(result.Report.Trend[^11].Segments, result.Report.PreviousSegments);
        Assert.Equal(2, result.Report.Trend[^1].Visitors);
        Assert.Equal(1, result.Report.Trend[0].Visitors);
    }

    [Fact]
    public void NewcomerRetentionCountsReturnsAndStays()
    {
        var data = new Channelish(20)
            .Attend("stayed", Range(5, 19))
            .Attend("came-back", [6, 12])
            .Attend("gone", [7])
            .Attend("too-fresh", [18]);
        var retention = data.Run().Report.Retention!;
        Assert.Equal(3, retention.Newcomers);
        Assert.Equal(2, retention.CameBack);
        Assert.Equal(1, retention.Stayed);
    }

    [Fact]
    public void SessionsOnTheSameLocalDayFormOneBroadcast()
    {
        var data = new Channelish(6);
        var restart = data.Sessions[^1];
        var second = new CommunitySessionSummary
        {
            StreamerTwitchUserId = Channel, StreamSessionId = Guid.NewGuid(), StartedAt = restart.EndedAt.AddMinutes(5),
            EndedAt = restart.EndedAt.AddMinutes(65), ExpectedMinutes = 60, CompleteMinutes = 60
        };
        data.Sessions.Add(second);
        data.Visits.Add(new() { StreamerTwitchUserId = Channel, StreamSessionId = restart.StreamSessionId, ViewerTwitchUserId = "split", Minutes = 6 });
        data.Visits.Add(new() { StreamerTwitchUserId = Channel, StreamSessionId = second.StreamSessionId, ViewerTwitchUserId = "split", Minutes = 10 });
        var result = data.Run();
        Assert.Equal(6, result.Report.TotalBroadcasts);
        Assert.Equal(1, Member(result, "split").VisitsInWindow);
        Assert.Equal(16, Member(result, "split").MinutesInWindow);
    }

    [Fact]
    public void CoverageCountsMissingMinutesAndTheLongestGap()
    {
        var start = new DateTime(2026, 11, 1, 18, 0, 30, DateTimeKind.Utc);
        var minutes = Enumerable.Range(0, 10).Where(i => i is not (3 or 4 or 5 or 8)).Select(i => start.AddMinutes(i).AddSeconds(-30));
        Assert.Equal((10, 6, 3), CommunityCalculator.Coverage(start, start.AddMinutes(9).AddSeconds(10), minutes));
    }
}
