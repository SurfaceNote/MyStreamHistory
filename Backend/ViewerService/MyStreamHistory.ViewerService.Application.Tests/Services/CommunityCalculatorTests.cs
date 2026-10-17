using MyStreamHistory.Shared.Base.Contracts.Community;
using MyStreamHistory.ViewerService.Application.Services;
using MyStreamHistory.ViewerService.Domain.Entities;
using Xunit;

namespace MyStreamHistory.ViewerService.Application.Tests.Services;

public class CommunityCalculatorTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly int[] Offsets = [0, 2, 4, 7, 9, 11, 14, 16, 18, 21];
    private static readonly Viewer User = new() { Id = Guid.NewGuid(), TwitchUserId = "1", Login = "quiet", DisplayName = "Quiet" };

    [Theory]
    [InlineData(6, 10, 3, "core")]
    [InlineData(5, 10, 3, "regular")]
    [InlineData(3, 10, 2, "regular")]
    [InlineData(3, 11, 2, "returning")]
    [InlineData(6, 10, 2, "regular")]
    [InlineData(3, 8, 1, "returning")]
    [InlineData(2, 10, 2, "returning")]
    [InlineData(1, 10, 1, "occasional")]
    [InlineData(0, 10, 0, "inactive")]
    [InlineData(6, 7, 3, "unknown")]
    public void ThresholdsIncludeBoundariesAndRequireDistinctWeeks(int visited, int eligible, int weeks, string expected) =>
        Assert.Equal(expected, CommunityCalculator.Classify(visited, eligible, weeks));

    [Fact]
    public void MessagePointsComeFromStoredPointsWithinTheReportWindow()
    {
        var data = Input(legacy: true);
        var expected = data.HistoricalWatches.Sum(w => w.ChatPoints);
        var oldCategory = Guid.NewGuid();
        var result = CommunityCalculator.Calculate(new()
        {
            Now = data.Now, Viewers = data.Viewers,
            Sessions = data.Sessions.Concat([new CommunitySession { Id = Guid.NewGuid(), StartedAt = Now.AddDays(-40), EndedAt = Now.AddDays(-40).AddHours(1), CategoryIds = [oldCategory] }]).ToList(),
            HistoricalWatches = data.HistoricalWatches.Concat([new ViewerCategoryWatch { ViewerId = User.Id, StreamCategoryId = oldCategory, ChatPoints = 999, MinutesWatched = 10 }]).ToList()
        });
        Assert.Equal(expected, Assert.Single(result.Members).MessagePoints);
        Assert.Null(result.Members[0].MessageCount);
    }

    [Fact]
    public void NoRecordedPointsAreUnknownRatherThanDerivedFromMessageCounts()
    {
        var result = CommunityCalculator.Calculate(Input());
        Assert.Null(Assert.Single(result.Members).MessagePoints);
    }

    [Fact]
    public void QuietConnectedParticipantCanBeCoreWithoutMessages()
    {
        var result = CommunityCalculator.Calculate(Input());
        Assert.Equal("verified", result.Report.Quality);
        Assert.Equal(1, result.Report.Core);
        Assert.Equal("core", Assert.Single(result.Members).Level);
        Assert.Equal(0, result.Members[0].MessageCount);
    }

    [Fact]
    public void HistoricalMinutesAreProvisionalAndNeverConvertedToMessageCounts()
    {
        var data = Input(legacy: true);
        var result = CommunityCalculator.Calculate(data);
        Assert.Equal("provisional", result.Report.Quality);
        var member = Assert.Single(result.Members);
        Assert.Equal("core", member.Level);
        Assert.Null(member.MessageCount);
        Assert.True(member.HasHistoricalChat);
        Assert.Equal("provisional", member.Quality);
        Assert.All(member.History, day => Assert.Equal("provisional", day.Quality));
    }

    [Fact]
    public void UnknownNewDayIsExcludedAndDoesNotManufactureAMissedVisit()
    {
        var data = Input(badPollDays: [0]);
        var result = CommunityCalculator.Calculate(data);
        Assert.Equal(9, result.Report.EligibleDays);
        Assert.Equal(1, result.Report.UnknownDays);
        Assert.Equal("unknown", result.Members[0].History[^1].Quality);
        Assert.False(result.Members[0].Declining);
    }

    [Fact]
    public void GapLongerThanThreeMinutesInvalidatesEvenCoverageAbove95Percent()
    {
        var data = Input(duration: 120);
        var newest = data.Sessions.OrderByDescending(s => s.StartedAt).First();
        var polls = data.Polls.Where(p => p.StreamSessionId != newest.Id || p.Minute < newest.StartedAt.AddMinutes(10) || p.Minute >= newest.StartedAt.AddMinutes(14)).ToList();
        var result = CommunityCalculator.Calculate(Copy(data, polls: polls));
        Assert.Equal(9, result.Report.EligibleDays);
        Assert.Equal(1, result.Report.UnknownDays);
    }

    [Fact]
    public void FourRepeatedMinuteRecordsDoNotBecomeFiveObservations()
    {
        var data = Input();
        var presence = data.Presence.Where(p => p.Minute.Minute < 4).ToList();
        presence.AddRange(presence);
        var result = CommunityCalculator.Calculate(Copy(data, presence: presence));
        Assert.Equal(0, result.Members[0].VisitedDays);
    }

    [Fact]
    public void ExclusionRemovesAccountFromCurrentAndPreviousWindows()
    {
        var data = Input();
        var result = CommunityCalculator.Calculate(new()
        {
            Now = data.Now, Sessions = data.Sessions, Presence = data.Presence, Polls = data.Polls,
            TrackingStartedAt = data.TrackingStartedAt, Viewers = data.Viewers, Exclusions = new HashSet<string> { "1" }
        });
        Assert.Empty(result.Members);
        Assert.Equal(0, result.Report.Core);
        Assert.All(result.Report.Trend.Where(p => p.Core != null), p => Assert.Equal(0, p.Core));
    }

    [Fact]
    public void NoStreamsReturnsNullMetricsRatherThanZeros()
    {
        var result = CommunityCalculator.Calculate(new() { Now = Now });
        Assert.Equal("insufficient", result.Report.Quality);
        Assert.Equal(84, result.Report.WindowDays);
        Assert.Null(result.Report.Core);
        Assert.Null(result.Report.Distribution);
        Assert.All(result.Report.Trend, p => Assert.Null(p.Core));
    }

    [Fact]
    public void WeeklyStreamerGetsExpandedWindowAndSameWindowForTrend()
    {
        var data = Input(offsets: [0, 7, 14, 21, 28, 35, 42, 49]);
        var result = CommunityCalculator.Calculate(data);
        Assert.Equal(56, result.Report.WindowDays);
        Assert.Equal(8, result.Report.EligibleDays);
        Assert.Equal(1, result.Report.Core);
        Assert.Null(result.Report.PreviousCore);
    }

    [Fact]
    public void MultipleSessionsOnOneDateCountAsOneOpportunity()
    {
        var data = Input(offsets: [0, 0, 2, 4, 7, 9, 11, 14, 16]);
        var result = CommunityCalculator.Calculate(data);
        Assert.Equal(8, result.Report.EligibleDays);
        Assert.Equal(8, result.Members[0].VisitedDays);
    }

    [Fact]
    public void OpenSessionMakesWholeBroadcastDayIncomplete()
    {
        var data = Input();
        data.Sessions[0].IsLive = true;
        data.Sessions[0].EndedAt = null;
        var result = CommunityCalculator.Calculate(data);
        Assert.Equal(9, result.Report.EligibleDays);
        Assert.DoesNotContain(result.Members[0].History, h => h.Date == new DateOnly(2026, 10, 16));
    }

    [Fact]
    public void DayShorterThan15MinutesIsExcluded()
    {
        var data = Input();
        data.Sessions[0].EndedAt = data.Sessions[0].StartedAt.AddMinutes(14);
        Assert.Equal(9, CommunityCalculator.Calculate(data).Report.EligibleDays);
    }

    [Fact]
    public void NightStreamBelongsToItsLocalStartDate()
    {
        var session = new CommunitySession { Id = Guid.NewGuid(), StartedAt = Now.Date.AddDays(-2).AddHours(23), EndedAt = Now.Date.AddDays(-1).AddHours(2) };
        var data = new CommunityCalculationInput
        {
            Now = Now, TimeZoneId = "Europe/Moscow", Sessions = [session], Viewers = [User],
            HistoricalWatches = [new() { ViewerId = User.Id, StreamCategoryId = Guid.Empty, MinutesWatched = 5 }]
        };
        session.CategoryIds.Add(Guid.Empty);
        var result = CommunityCalculator.Calculate(data);
        Assert.Equal(new DateOnly(2026, 10, 16), Assert.Single(result.Members[0].History).Date);
    }

    [Fact]
    public void CutoverDayIsNotInventedAsCompleteHistoricalData()
    {
        var data = Input();
        var result = CommunityCalculator.Calculate(new()
        {
            Now = data.Now, Sessions = data.Sessions, Viewers = data.Viewers,
            TrackingStartedAt = data.Sessions[0].StartedAt.AddMinutes(15),
            HistoricalWatches = data.Sessions.Select(s => new ViewerCategoryWatch { ViewerId = User.Id, StreamCategoryId = s.CategoryIds[0], MinutesWatched = 30 }).ToList()
        });
        Assert.Equal(1, result.Report.UnknownDays);
        Assert.Equal(9, result.Report.EligibleDays);
    }

    [Fact]
    public void RecalculationDoesNotReclassifySameDayEntryAsRestoredCore()
    {
        var data = Input();
        var first = CommunityCalculator.Calculate(data);
        var dates = first.DailyStates.Where(s => s.Member.Level == "core").GroupBy(s => s.Member.TwitchUserId).ToDictionary(g => g.Key, g => g.Min(x => x.Date));
        var second = CommunityCalculator.Calculate(new()
        {
            Now = data.Now, Sessions = data.Sessions, Presence = data.Presence, Polls = data.Polls,
            TrackingStartedAt = data.TrackingStartedAt, Viewers = data.Viewers, KnownCoreDates = dates
        });
        Assert.Equal(first.Report.Transitions.GetValueOrDefault("enteredCore"), second.Report.Transitions.GetValueOrDefault("enteredCore"));
    }

    private static CommunityCalculationInput Copy(CommunityCalculationInput d, List<CommunityPoll>? polls = null, List<CommunityPresence>? presence = null) => new()
    {
        Now = d.Now, TrackingStartedAt = d.TrackingStartedAt, Sessions = d.Sessions, Viewers = d.Viewers,
        Polls = polls ?? d.Polls, Presence = presence ?? d.Presence
    };

    [Fact]
    public void ThreeVerifiedMissesFlagPriorRegularParticipant()
    {
        var data = Input(offsets: [0, 1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24]);
        var recent = data.Sessions.Where(s => s.StartedAt.Date >= Now.Date.AddDays(-3)).Select(s => s.Id).ToHashSet();
        var result = CommunityCalculator.Calculate(Copy(data, presence: data.Presence.Where(p => !recent.Contains(p.StreamSessionId)).ToList()));
        Assert.True(result.Members[0].Declining);
        Assert.Equal(3, result.Members[0].ConsecutiveMisses);
        Assert.False(result.Members[0].Dormant);
    }

    [Fact]
    public void UnknownDayBreaksConsecutiveMisses()
    {
        var data = Input(offsets: [0, 1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24], badPollDays: [1]);
        var recent = data.Sessions.Take(3).Select(s => s.Id).ToHashSet();
        var result = CommunityCalculator.Calculate(Copy(data, presence: data.Presence.Where(p => !recent.Contains(p.StreamSessionId)).ToList()));
        Assert.Equal(1, result.Members[0].ConsecutiveMisses);
        Assert.False(result.Members[0].Declining);
    }

    [Fact]
    public void ParticipationAfterSixVerifiedMissesIsAReturn()
    {
        var data = Input(offsets: [0, 1, 2, 3, 4, 5, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24]);
        var missing = data.Sessions.Skip(1).Take(6).Select(s => s.Id).ToHashSet();
        var result = CommunityCalculator.Calculate(Copy(data, presence: data.Presence.Where(p => !missing.Contains(p.StreamSessionId)).ToList()));
        Assert.Equal("returned", result.Members[0].Transition);
        Assert.False(result.Members[0].Declining);
    }

    [Fact]
    public void SixVerifiedMissesMakeParticipantDormant()
    {
        var data = Input(offsets: [0, 1, 2, 3, 4, 5, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24]);
        var missing = data.Sessions.Take(6).Select(s => s.Id).ToHashSet();
        var result = CommunityCalculator.Calculate(Copy(data, presence: data.Presence.Where(p => !missing.Contains(p.StreamSessionId)).ToList()));
        Assert.True(result.Members[0].Dormant);
        Assert.Equal(6, result.Members[0].ConsecutiveMisses);
    }

    [Fact]
    public void MessagesAloneDoNotRaiseConnectionRegularity()
    {
        var data = Input();
        var result = CommunityCalculator.Calculate(new()
        {
            Now = data.Now, TrackingStartedAt = data.TrackingStartedAt, Sessions = data.Sessions,
            Viewers = data.Viewers, Polls = data.Polls,
            Messages = data.Sessions.Select(s => new CommunityMessageAggregate { ViewerTwitchUserId = "1", StreamSessionId = s.Id, Count = 100 }).ToList()
        });
        Assert.Equal("inactive", result.Members[0].Level);
        Assert.Equal(0, result.Members[0].VisitedDays);
        Assert.Equal(1000, result.Members[0].MessageCount);
    }

    private static CommunityCalculationInput Input(bool legacy = false, int[]? offsets = null, int duration = 30, int[]? badPollDays = null)
    {
        var sessions = (offsets ?? Offsets).Select((offset, i) => new CommunitySession
        {
            Id = Guid.NewGuid(), StartedAt = Now.Date.AddDays(-1 - offset).AddHours(18).AddMinutes(i),
            CategoryIds = [Guid.NewGuid()]
        }).ToList();
        foreach (var s in sessions) s.EndedAt = s.StartedAt.AddMinutes(duration);
        var polls = sessions.SelectMany((s, i) => Enumerable.Range(0, duration).Select(n => new CommunityPoll
        {
            StreamSessionId = s.Id, Minute = s.StartedAt.AddMinutes(n), Complete = !(badPollDays ?? []).Contains(i)
        })).ToList();
        var presence = sessions.SelectMany(s => Enumerable.Range(0, 5).Select(n => new CommunityPresence
        {
            StreamSessionId = s.Id, ViewerTwitchUserId = "1", Minute = s.StartedAt.AddMinutes(n)
        })).ToList();
        return new()
        {
            Now = Now, Viewers = [User], Sessions = sessions, Polls = legacy ? [] : polls, Presence = legacy ? [] : presence,
            TrackingStartedAt = legacy ? null : Now.AddDays(-200),
            HistoricalWatches = legacy ? sessions.Select(s => new ViewerCategoryWatch { ViewerId = User.Id, StreamCategoryId = s.CategoryIds[0], MinutesWatched = 5, ChatPoints = 1 }).ToList() : []
        };
    }
}
