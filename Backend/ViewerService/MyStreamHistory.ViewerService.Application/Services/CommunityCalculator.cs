using MyStreamHistory.Shared.Base.Contracts.Community;
using MyStreamHistory.ViewerService.Domain.Entities;
using S = MyStreamHistory.Shared.Base.Contracts.Community.CommunitySegments;

namespace MyStreamHistory.ViewerService.Application.Services;

public sealed record CommunityProfile(string TwitchUserId, string DisplayName, string Login, string? ProfileImageUrl);

public sealed class CommunityCalculationInput
{
    public DateTime Now { get; init; }
    public string TimeZoneId { get; init; } = "UTC";
    public string StreamerTwitchUserId { get; init; } = "";
    public IReadOnlyList<CommunitySessionSummary> Sessions { get; init; } = [];
    public IReadOnlyList<CommunitySessionVisit> Visits { get; init; } = [];
    public IReadOnlyList<CommunityMessageAggregate> Messages { get; init; } = [];
    public IReadOnlyList<CommunityProfile> Profiles { get; init; } = [];
    public IReadOnlySet<string> Exclusions { get; init; } = new HashSet<string>();
    /// <summary>Bot logins (lower case) that never belong to an audience.</summary>
    public IReadOnlySet<string> ExcludedLogins { get; init; } = new HashSet<string>();
}

public sealed record CommunityCalculation(CommunityReport Report, List<CommunityMember> Members);

/// <summary>
/// Pure, deterministic segmentation of a channel's chat audience. The unit is a broadcast (sessions grouped by their
/// local start date); the window is the last <see cref="WindowSize"/> broadcasts with usable data, so the result does
/// not depend on how often the channel streams. Chat connection is never treated as confirmed video viewing.
/// </summary>
public static class CommunityCalculator
{
    public const int WindowSize = 20;
    public const int MinBroadcasts = 5;
    public const int TrendLength = 60;
    public const int RecentBroadcasts = 3;
    public const int LostAfter = 10;
    public const int ForgetAfterDays = 90;
    public const int ComebackGap = 6;
    public const int ComebackDays = 21;
    public const int ComebackHistory = 3;
    public const int FadingCore = 3;
    public const int FadingRegular = 5;
    public const int CompareOffset = 10;
    public const decimal MinCoverage = 0.8m;
    public const int MinBroadcastMinutes = 15;
    private const int ListLimit = 12;

    private sealed record VisitData(int Minutes, int Messages, bool Chatted);

    private sealed class Broadcast
    {
        public DateOnly Date { get; init; }
        public double Minutes { get; init; }
        public bool Legacy { get; init; }
        public decimal? Coverage { get; init; }
        public bool Eligible => Minutes >= MinBroadcastMinutes && (Legacy || Coverage >= MinCoverage);
    }

    private sealed class Track(string id)
    {
        public string Id { get; } = id;
        public List<int> Visits { get; } = [];
        public List<int> Comebacks { get; } = [];
        public Dictionary<int, VisitData> Data { get; } = [];
        public int First => Visits[0];

        /// <summary>Number of visits at broadcast indices up to and including <paramref name="k"/>.</summary>
        public int CountUpTo(int k)
        {
            if (k < 0) return 0;
            int lo = 0, hi = Visits.Count;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (Visits[mid] <= k) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        public int InRange(int from, int to) => to < from ? 0 : CountUpTo(to) - CountUpTo(from - 1);
        public bool Visited(int k) => Data.ContainsKey(k);
    }

    /// <summary>A visit needs at least ten minutes in chat, or a tenth of a long broadcast, or a chat message.</summary>
    public static int VisitThreshold(double broadcastMinutes) => Math.Max(10, (int)Math.Ceiling(broadcastMinutes * 0.1));

    public static CommunityCalculation Calculate(CommunityCalculationInput input)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(input.TimeZoneId);
        DateOnly LocalDate(DateTime at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(at, DateTimeKind.Utc), zone));

        // 1. Broadcasts: sessions grouped by local start date.
        var all = input.Sessions.GroupBy(s => LocalDate(s.StartedAt)).OrderBy(g => g.Key).Select(g =>
        {
            var tracked = g.Where(s => !s.Legacy).ToList();
            var expected = tracked.Sum(s => s.ExpectedMinutes);
            return (Sessions: g.ToList(), Broadcast: new Broadcast
            {
                Date = g.Key,
                Minutes = g.Sum(s => Math.Max(0, (s.EndedAt - s.StartedAt).TotalMinutes)),
                Legacy = tracked.Count == 0,
                Coverage = expected == 0 ? (tracked.Count == 0 ? null : 0m) : Math.Round((decimal)tracked.Sum(s => s.CompleteMinutes) / expected, 4)
            });
        }).ToList();
        var broadcasts = new List<Broadcast>();
        var sessionIndex = new Dictionary<Guid, int>();
        foreach (var (sessions, broadcast) in all.Where(b => b.Broadcast.Eligible))
        {
            foreach (var session in sessions) sessionIndex[session.StreamSessionId] = broadcasts.Count;
            broadcasts.Add(broadcast);
        }

        // 2. Per viewer, per broadcast: minutes and messages.
        var profiles = input.Profiles.GroupBy(p => p.TwitchUserId).ToDictionary(g => g.Key, g => g.First());
        bool Excluded(string id) => id == input.StreamerTwitchUserId || input.Exclusions.Contains(id)
            || profiles.TryGetValue(id, out var p) && input.ExcludedLogins.Contains(p.Login.ToLowerInvariant());
        var raw = new Dictionary<(string Viewer, int Broadcast), (int Minutes, int Messages, bool Chatted)>();
        foreach (var visit in input.Visits)
        {
            if (!sessionIndex.TryGetValue(visit.StreamSessionId, out var index) || Excluded(visit.ViewerTwitchUserId)) continue;
            var entry = raw.GetValueOrDefault((visit.ViewerTwitchUserId, index));
            raw[(visit.ViewerTwitchUserId, index)] = (entry.Minutes + visit.Minutes, entry.Messages, entry.Chatted || visit.LegacyChat);
        }
        foreach (var message in input.Messages)
        {
            if (message.Count <= 0 || !sessionIndex.TryGetValue(message.StreamSessionId, out var index) || Excluded(message.ViewerTwitchUserId)) continue;
            var entry = raw.GetValueOrDefault((message.ViewerTwitchUserId, index));
            raw[(message.ViewerTwitchUserId, index)] = (entry.Minutes, entry.Messages + message.Count, true);
        }
        var tracks = new Dictionary<string, Track>();
        foreach (var ((viewer, index), value) in raw.OrderBy(x => x.Key.Broadcast))
        {
            if (value.Minutes < VisitThreshold(broadcasts[index].Minutes) && !value.Chatted) continue;
            if (!tracks.TryGetValue(viewer, out var track)) tracks[viewer] = track = new Track(viewer);
            track.Visits.Add(index);
            track.Data[index] = new VisitData(value.Minutes, value.Messages, value.Chatted);
        }
        foreach (var track in tracks.Values)
            for (var i = 1; i < track.Visits.Count; i++)
            {
                int p = track.Visits[i - 1], q = track.Visits[i];
                if (q - p - 1 >= ComebackGap && broadcasts[q].Date.DayNumber - broadcasts[p].Date.DayNumber >= ComebackDays && i >= ComebackHistory)
                    track.Comebacks.Add(q);
            }

        var n = broadcasts.Count;
        var report = new CommunityReport
        {
            CalculatedAt = input.Now, TimeZoneId = input.TimeZoneId, WindowSize = WindowSize, MinBroadcasts = MinBroadcasts,
            TotalBroadcasts = n
        };
        if (n == 0)
        {
            report.SkippedBroadcasts = all.Count;
            return new(report, []);
        }

        // 3. Segment rules, evaluated at any broadcast index.
        int Len(int k) => Math.Min(WindowSize, k + 1);
        string? RatioLevel(Track t, int j)
        {
            if (j + 1 < MinBroadcasts) return null;
            var size = Len(j);
            var count = t.InRange(j - size + 1, j);
            if (count * 100 >= size * 60 && t.InRange(j - (RecentBroadcasts - 1), j) > 0) return S.Core;
            if (count * 100 >= size * 30 && count >= 2 && t.InRange(j - (FadingRegular - 1), j) > 0) return S.Regular;
            return null;
        }
        string? Segment(Track t, int k)
        {
            var count = t.CountUpTo(k);
            if (count == 0) return null;
            var last = t.Visits[count - 1];
            if (broadcasts[k].Date.DayNumber - broadcasts[last].Date.DayNumber > ForgetAfterDays) return null;
            if (t.Comebacks.Any(c => c <= k && c > k - RecentBroadcasts)) return S.Returned;
            if (t.First > k - RecentBroadcasts) return S.New;
            var streak = k - last;
            if (streak >= LostAfter && count >= 2) return S.Lost;
            var level = RatioLevel(t, last);
            if (level == S.Core && streak >= FadingCore || level == S.Regular && streak >= FadingRegular) return S.Fading;
            return RatioLevel(t, k) ?? (count >= 2 ? S.Occasional : S.OneTime);
        }
        bool LegacyInWindow(int k) => Enumerable.Range(k - Len(k) + 1, Len(k)).Any(i => broadcasts[i].Legacy);

        // 4. Trend: segment sizes and flows per broadcast.
        var current = n - 1;
        var firstTrend = Math.Max(0, n - TrendLength);
        var segmentsAt = new Dictionary<int, Dictionary<string, string>>();
        Dictionary<string, string> SegmentsAt(int k)
        {
            if (segmentsAt.TryGetValue(k, out var cached)) return cached;
            var result = new Dictionary<string, string>();
            foreach (var t in tracks.Values)
                if (Segment(t, k) is { } value) result[t.Id] = value;
            return segmentsAt[k] = result;
        }
        for (var k = firstTrend; k <= current; k++)
        {
            var now = SegmentsAt(k);
            var previous = k > 0 ? SegmentsAt(k - 1) : [];
            if (k > 1) segmentsAt.Remove(k - 2);
            report.Trend.Add(new CommunityTrendPoint
            {
                Date = broadcasts[k].Date,
                Segments = S.All.ToDictionary(s => s, s => now.Values.Count(v => v == s)),
                Visitors = tracks.Values.Count(t => t.Visited(k)),
                NewViewers = tracks.Values.Count(t => t.First == k),
                Returned = tracks.Values.Count(t => t.Comebacks.Contains(k)),
                BecameFading = now.Count(x => x.Value == S.Fading && previous.GetValueOrDefault(x.Key) != S.Fading),
                BecameLost = now.Count(x => x.Value == S.Lost && previous.GetValueOrDefault(x.Key) != S.Lost),
                Provisional = k + 1 < WindowSize || LegacyInWindow(k)
            });
        }

        // 5. Current state.
        var windowLength = Len(current);
        var from = current - windowLength + 1;
        var segments = SegmentsAt(current);
        var previousSegments = current > 0 ? SegmentsAt(current - 1) : [];
        report.WindowDates = broadcasts.Skip(from).Select(b => b.Date).ToList();
        report.From = broadcasts[from].Date;
        report.AsOf = broadcasts[current].Date;
        report.SkippedBroadcasts = all.Count(b => !b.Broadcast.Eligible && b.Broadcast.Date >= report.From);
        var coverage = broadcasts.Skip(from).Where(b => b.Coverage.HasValue).Select(b => b.Coverage!.Value).ToList();
        report.Coverage = coverage.Count == 0 ? null : Math.Round(coverage.Average(), 4);
        report.Quality = n < MinBroadcasts ? "insufficient" : windowLength < WindowSize || LegacyInWindow(current) ? "provisional" : "verified";
        report.Segments = S.All.ToDictionary(s => s, s => segments.Values.Count(v => v == s));
        if (current - CompareOffset >= 0)
        {
            var compared = current - CompareOffset;
            report.PreviousDate = broadcasts[compared].Date;
            report.PreviousSegments = report.Trend.FirstOrDefault(p => p.Date == broadcasts[compared].Date)?.Segments
                ?? S.All.ToDictionary(s => s, s => tracks.Values.Count(t => Segment(t, compared) == s));
        }

        CommunityAccount Account(string id) => new() { TwitchUserId = id, DisplayName = profiles.GetValueOrDefault(id)?.DisplayName ?? id };
        List<CommunityAccount> Accounts(IEnumerable<Track> source) =>
            source.OrderByDescending(t => t.Data.GetValueOrDefault(current)?.Minutes ?? 0).ThenBy(t => t.Id, StringComparer.Ordinal)
                .Take(ListLimit).Select(t => Account(t.Id)).ToList();
        var newcomers = tracks.Values.Where(t => t.First == current).ToList();
        var returned = tracks.Values.Where(t => t.Comebacks.Contains(current)).ToList();
        var missingCore = tracks.Values.Where(t => previousSegments.GetValueOrDefault(t.Id) == S.Core && !t.Visited(current))
            .OrderByDescending(t => t.InRange(from, current)).ThenBy(t => t.Id, StringComparer.Ordinal).ToList();
        report.LastBroadcast = new CommunityLastBroadcast
        {
            Date = broadcasts[current].Date,
            Visitors = tracks.Values.Count(t => t.Visited(current)),
            NewCount = newcomers.Count, ReturnedCount = returned.Count, MissingCoreCount = missingCore.Count,
            New = Accounts(newcomers), Returned = Accounts(returned),
            MissingCore = missingCore.Take(ListLimit).Select(t => Account(t.Id)).ToList()
        };
        if (current - (RecentBroadcasts - 1) > from)
        {
            var cohort = tracks.Values.Where(t => t.First >= from && t.First <= current - RecentBroadcasts).ToList();
            report.Retention = new CommunityRetention
            {
                Newcomers = cohort.Count,
                CameBack = cohort.Count(t => t.CountUpTo(current) >= 2),
                Stayed = cohort.Count(t => segments.GetValueOrDefault(t.Id) is S.Core or S.Regular)
            };
        }

        // 6. Members.
        var windowAllLegacy = broadcasts.Skip(from).All(b => b.Legacy);
        var members = new List<CommunityMember>();
        foreach (var (id, segment) in segments)
        {
            var t = tracks[id];
            var visits = Enumerable.Range(from, windowLength).Where(t.Visited).Select(i => t.Data[i]).ToList();
            var chatVisits = visits.Count(v => v.Chatted);
            var profile = profiles.GetValueOrDefault(id);
            var lastIndex = t.Visits[t.CountUpTo(current) - 1];
            members.Add(new CommunityMember
            {
                TwitchUserId = id, DisplayName = profile?.DisplayName ?? id, Login = profile?.Login ?? id, ProfileImageUrl = profile?.ProfileImageUrl,
                Segment = segment,
                Activity = visits.Count >= 2 && chatVisits * 2 >= visits.Count ? S.Chatty : visits.Count >= 3 && chatVisits == 0 ? S.Silent : null,
                VisitsInWindow = visits.Count, WindowSize = windowLength, TotalVisits = t.CountUpTo(current),
                MissedStreak = current - lastIndex,
                MessagesInWindow = windowAllLegacy ? null : visits.Sum(v => v.Messages),
                MinutesInWindow = visits.Sum(v => v.Minutes),
                FirstSeen = broadcasts[t.First].Date, LastSeen = broadcasts[lastIndex].Date,
                Attendance = string.Concat(Enumerable.Range(from, windowLength).Select(i => t.Visited(i) ? '1' : '0'))
            });
        }
        report.Activity = new Dictionary<string, int>
        {
            [S.Chatty] = members.Count(m => m.Activity == S.Chatty),
            [S.Silent] = members.Count(m => m.Activity == S.Silent)
        };
        return new(report, Order(members).ToList());
    }

    /// <summary>Most present first, then most recent.</summary>
    public static IEnumerable<CommunityMember> Order(IEnumerable<CommunityMember> members) => members
        .OrderByDescending(m => m.VisitsInWindow).ThenByDescending(m => m.TotalVisits).ThenByDescending(m => m.LastSeen)
        .ThenBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.TwitchUserId, StringComparer.Ordinal);

    public static DateTime FloorMinute(DateTime value) => new(value.Ticks - value.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);

    /// <summary>Coverage of a finished tracked session from its complete minute polls.</summary>
    public static (int Expected, int Complete, int LongestGap) Coverage(DateTime startedAt, DateTime endedAt, IEnumerable<DateTime> completeMinutes)
    {
        var complete = completeMinutes.ToHashSet();
        int expected = 0, successful = 0, gap = 0, longest = 0;
        for (var minute = FloorMinute(startedAt); minute < endedAt; minute = minute.AddMinutes(1))
        {
            expected++;
            if (complete.Contains(minute)) { successful++; gap = 0; }
            else longest = Math.Max(longest, ++gap);
        }
        return (expected, successful, longest);
    }
}
