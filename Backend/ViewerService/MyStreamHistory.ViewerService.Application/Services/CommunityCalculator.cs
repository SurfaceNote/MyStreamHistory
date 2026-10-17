using MyStreamHistory.Shared.Base.Contracts.Community;
using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Application.Services;

public sealed class CommunityCalculationInput
{
    public DateTime Now { get; init; }
    public string TimeZoneId { get; init; } = "UTC";
    public DateTime? TrackingStartedAt { get; init; }
    public IReadOnlyList<CommunitySession> Sessions { get; init; } = [];
    public IReadOnlyList<CommunityPoll> Polls { get; init; } = [];
    public IReadOnlyList<CommunityPresence> Presence { get; init; } = [];
    public IReadOnlyList<CommunityMessageAggregate> Messages { get; init; } = [];
    public IReadOnlyList<ViewerCategoryWatch> HistoricalWatches { get; init; } = [];
    public IReadOnlyList<Viewer> Viewers { get; init; } = [];
    public IReadOnlySet<string> Exclusions { get; init; } = new HashSet<string>();
    public IReadOnlyDictionary<string, DateOnly> KnownCoreDates { get; init; } = new Dictionary<string, DateOnly>();
}

public sealed record CommunityCalculation(CommunityReport Report, List<CommunityMember> Members,
    List<(DateOnly Date, CommunityMember Member)> DailyStates);

/// <summary>Pure, deterministic calculation. Chat connection is never treated as video viewing.</summary>
public static class CommunityCalculator
{
    private sealed class Day
    {
        public DateOnly Date { get; init; }
        public string Quality { get; init; } = "unknown";
        public decimal? Coverage { get; init; }
        public Dictionary<string, int> Visits { get; init; } = [];
        public Dictionary<string, (int Count, bool Provisional)> Messages { get; init; } = [];
        public Dictionary<string, decimal> MessagePoints { get; init; } = [];
        public HashSet<string> HistoricalChat { get; init; } = [];
        public bool Eligible => Quality is "verified" or "provisional";
        public bool Visited(string id) => Visits.GetValueOrDefault(id) >= 5;
    }

    public static string Classify(int visited, int eligible, int weeks)
    {
        if (eligible < 8) return "unknown";
        if (visited >= 4 && visited * 100 >= eligible * 60 && weeks >= 3) return "core";
        if (visited >= 3 && visited * 100 >= eligible * 30 && weeks >= 2) return "regular";
        if (visited >= 2) return "returning";
        return visited == 1 ? "occasional" : "inactive";
    }

    public static CommunityCalculation Calculate(CommunityCalculationInput input)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(input.TimeZoneId);
        DateOnly LocalDate(DateTime at) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(at, DateTimeKind.Utc), zone));
        var asOf = LocalDate(input.Now).AddDays(-1);
        var sessions = input.Sessions.ToDictionary(s => s.Id);
        var viewers = input.Viewers.ToDictionary(v => v.Id);
        var historical = input.HistoricalWatches.Where(w => viewers.ContainsKey(w.ViewerId))
            .Select(w => (Watch: w, User: viewers[w.ViewerId].TwitchUserId)).ToList();
        var pollBySession = input.Polls.ToLookup(p => p.StreamSessionId);
        var presenceBySession = input.Presence.ToLookup(p => p.StreamSessionId);
        var messagesBySession = input.Messages.ToLookup(m => m.StreamSessionId);
        var watchesByCategory = historical.ToLookup(w => w.Watch.StreamCategoryId);
        var days = new List<Day>();
        foreach (var group in input.Sessions.GroupBy(s => LocalDate(s.StartedAt)).OrderBy(g => g.Key))
        {
            if (group.Key > asOf || group.Any(s => s.IsLive || !s.EndedAt.HasValue)) continue;
            if (group.Sum(s => Math.Max(0, (s.EndedAt!.Value - s.StartedAt).TotalMinutes)) < 15) continue;
            var legacy = !input.TrackingStartedAt.HasValue || group.All(s => s.EndedAt <= input.TrackingStartedAt);
            var expected = new HashSet<DateTime>();
            var successful = new HashSet<DateTime>();
            // Old days require no synthetic coverage or minute reconstruction.
            if (!legacy)
            {
                foreach (var s in group)
                {
                    for (var minute = FloorMinute(s.StartedAt); minute < s.EndedAt!.Value; minute = minute.AddMinutes(1)) expected.Add(minute);
                    foreach (var poll in pollBySession[s.Id].Where(p => p.Complete)) successful.Add(poll.Minute);
                }
                successful.IntersectWith(expected);
            }
            var coverage = expected.Count == 0 ? (decimal?)null : (decimal)successful.Count / expected.Count;
            var longestGap = 0;
            var gap = 0;
            DateTime? previous = null;
            foreach (var minute in expected.Order())
            {
                if (previous.HasValue && minute != previous.Value.AddMinutes(1)) gap = 0;
                gap = successful.Contains(minute) ? 0 : gap + 1;
                longestGap = Math.Max(longestGap, gap);
                previous = minute;
            }
            var quality = legacy ? "provisional" : coverage >= .95m && longestGap <= 3 ? "verified" : "unknown";
            var dayWatches = group.SelectMany(s => s.CategoryIds.SelectMany(id => watchesByCategory[id])).ToList();
            var visits = legacy
                ? dayWatches.GroupBy(w => w.User).ToDictionary(g => g.Key, g => g.Sum(w => w.Watch.MinutesWatched))
                : group.SelectMany(s => presenceBySession[s.Id]).GroupBy(p => p.ViewerTwitchUserId)
                    .ToDictionary(g => g.Key, g => g.Select(p => p.Minute).Distinct().Count());
            days.Add(new Day
            {
                Date = group.Key, Quality = quality, Coverage = coverage, Visits = visits,
                MessagePoints = dayWatches.GroupBy(w => w.User).ToDictionary(g => g.Key, g => g.Sum(w => w.Watch.ChatPoints)),
                HistoricalChat = dayWatches.Where(w => w.Watch.ChatPoints > 0).Select(w => w.User).ToHashSet(),
                Messages = group.SelectMany(s => messagesBySession[s.Id]).GroupBy(m => m.ViewerTwitchUserId)
                    .ToDictionary(g => g.Key, g => (g.Sum(m => m.Count), g.Any(m => m.Provisional)))
            });
        }

        var ids = input.Viewers.Select(v => v.TwitchUserId)
            .Concat(days.SelectMany(d => d.Visits.Keys)).Concat(days.SelectMany(d => d.Messages.Keys))
            .Where(id => !input.Exclusions.Contains(id))
            .Where(id => days.Any(d => d.Visits.GetValueOrDefault(id) > 0 || d.Messages.ContainsKey(id) || d.HistoricalChat.Contains(id)))
            .Distinct().Order(StringComparer.Ordinal).ToList();
        var profiles = input.Viewers.GroupBy(v => v.TwitchUserId).ToDictionary(g => g.Key, g => g.First());
        var window = 28;
        while (window < 84 && days.Count(d => d.Eligible && d.Date <= asOf && d.Date > asOf.AddDays(-window)) < 8) window += 7;
        var memo = new Dictionary<DateOnly, List<CommunityMember>>();
        var knownCoreDates = input.KnownCoreDates.ToDictionary(x => x.Key, x => x.Value);
        List<CommunityMember> Evaluate(DateOnly end)
        {
            if (memo.TryGetValue(end, out var result)) return result;
            var relevant = days.Where(d => d.Date <= end && d.Date > end.AddDays(-window)).ToList();
            var eligible = relevant.Where(d => d.Eligible).ToList();
            var quality = eligible.Count < 8 ? "insufficient" : eligible.Any(d => d.Quality == "provisional") ? "provisional" : "verified";
            result = ids.Select(id =>
            {
                var visited = eligible.Where(d => d.Visited(id)).ToList();
                var messageDays = relevant.Where(d => d.Messages.ContainsKey(id)).ToList();
                var oldChat = relevant.Any(d => d.HistoricalChat.Contains(id));
                var hasLegacy = relevant.Any(d => d.Quality == "provisional");
                profiles.TryGetValue(id, out var profile);
                return new CommunityMember
                {
                    TwitchUserId = id, DisplayName = profile?.DisplayName ?? id, Login = profile?.Login ?? id,
                    ProfileImageUrl = profile?.ProfileImageUrl,
                    Level = Classify(visited.Count, eligible.Count, visited.Select(d => WeekStart(d.Date)).Distinct().Count()),
                    Quality = quality, VisitedDays = visited.Count, EligibleDays = eligible.Count,
                    Regularity = eligible.Count == 0 ? null : Math.Round((decimal)visited.Count / eligible.Count, 4),
                    MessageCount = hasLegacy ? null : messageDays.Sum(d => d.Messages[id].Count),
                    MessagePoints = relevant.Any(d => d.MessagePoints.ContainsKey(id)) ? relevant.Sum(d => d.MessagePoints.GetValueOrDefault(id)) : null,
                    DaysWithMessages = hasLegacy ? null : messageDays.Count,
                    HasHistoricalChat = oldChat,
                    MessagesProvisional = hasLegacy || messageDays.Any(d => d.Messages[id].Provisional),
                    FirstObserved = days.Where(d => d.Date <= end && (d.Visits.GetValueOrDefault(id) > 0 || d.Messages.ContainsKey(id) || d.HistoricalChat.Contains(id)))
                        .Select(d => (DateOnly?)d.Date).FirstOrDefault(),
                    History = days.Where(d => d.Date <= end).TakeLast(12).Select(d => new CommunityDayHistory
                    {
                        Date = d.Date, Quality = d.Quality,
                        Status = d.Visited(id) ? "visited" : d.Quality == "verified" ? "notObserved" : "unknown"
                    }).ToList()
                };
            }).ToList();
            memo[end] = result;
            return result;
        }

        void AddChanges(DateOnly end, List<CommunityMember> members)
        {
            var prior = Evaluate(end.AddDays(-1)).ToDictionary(m => m.TwitchUserId);
            var completed = days.Where(d => d.Date <= end).ToList();
            foreach (var member in members)
            {
                var id = member.TwitchUserId;
                var streak = completed.AsEnumerable().Reverse().TakeWhile(d => d.Quality == "verified" && !d.Visited(id)).ToList();
                if (streak.Count > 0)
                {
                    var baseline = Evaluate(streak[^1].Date.AddDays(-1)).First(m => m.TwitchUserId == id);
                    member.ConsecutiveMisses = streak.Count;
                    member.Declining = streak.Count >= 3 && baseline.Level is "core" or "regular";
                    member.Dormant = streak.Count >= 6 && member.Declining;
                    if (member.Declining && baseline.Quality != "verified") member.Quality = "provisional";
                }
                var latest = completed.LastOrDefault();
                // No decline is manufactured by an outage or mere passage of calendar time.
                if (latest?.Date != end || latest.Quality == "unknown" || member.Quality == "insufficient") continue;
                var before = prior[id];
                if (before.Quality != "insufficient" && member.Level != before.Level)
                {
                    member.Transition = member.Level == "core" ? knownCoreDates.TryGetValue(id, out var coreDate) && coreDate < end ? "restoredCore" : "enteredCore"
                        : before.Level == "core" ? "leftCore"
                        : member.Level == "regular" && before.Level is not ("core" or "regular") ? "becameRegular" : null;
                    if (member.Transition != null && before.Quality != "verified") member.Quality = "provisional";
                }
                if (latest.Quality == "verified" && latest.Visited(id))
                {
                    var missed = completed.Take(completed.Count - 1).Reverse().TakeWhile(d => d.Quality == "verified" && !d.Visited(id)).ToList();
                    if (missed.Count >= 6)
                    {
                        var baseline = Evaluate(missed[^1].Date.AddDays(-1)).First(m => m.TwitchUserId == id);
                        if (baseline.Level is "core" or "regular")
                        {
                            member.Returned = true;
                            member.Transition ??= "returned";
                            if (baseline.Quality != "verified") member.Quality = "provisional";
                        }
                    }
                }
            }
        }

        var current = Evaluate(asOf);
        var selectedDays = days.Where(d => d.Date <= asOf && d.Date > asOf.AddDays(-window)).ToList();
        var eligibleDays = selectedDays.Where(d => d.Eligible).ToList();
        var sufficient = eligibleDays.Count >= 8;
        var previousPeriod = Evaluate(asOf.AddDays(-window));
        var states = new List<(DateOnly, CommunityMember)>();
        var trend = new List<CommunityTrendPoint>();
        for (var date = asOf.AddDays(-27); date <= asOf; date = date.AddDays(1))
        {
            var members = Evaluate(date);
            AddChanges(date, members);
            foreach (var member in members.Where(m => m.Level == "core"))
                if (!knownCoreDates.TryGetValue(member.TwitchUserId, out var firstCore) || date < firstCore) knownCoreDates[member.TwitchUserId] = date;
            var count = days.Count(d => d.Eligible && d.Date <= date && d.Date > date.AddDays(-window));
            var quality = count < 8 ? "insufficient" : days.Any(d => d.Eligible && d.Quality == "provisional" && d.Date <= date && d.Date > date.AddDays(-window)) ? "provisional" : "verified";
            trend.Add(new() { Date = date, Core = count < 8 ? null : members.Count(m => m.Level == "core"), Quality = quality });
            states.AddRange(members.Select(m => (date, m)));
        }
        var verifiedCoverage = selectedDays.Where(d => d.Coverage.HasValue).Select(d => d.Coverage!.Value).ToList();
        return new(new CommunityReport
        {
            CalculatedAt = input.Now, AsOf = asOf, From = asOf.AddDays(1 - window), WindowDays = window,
            TimeZoneId = input.TimeZoneId, EligibleDays = eligibleDays.Count,
            UnknownDays = selectedDays.Count(d => !d.Eligible),
            Quality = !sufficient ? "insufficient" : eligibleDays.Any(d => d.Quality == "provisional")
                || current.Any(m => m.Quality == "provisional") || previousPeriod.Any(m => m.Quality == "provisional")
                || trend.Any(p => p.Quality == "provisional") ? "provisional" : "verified",
            Coverage = verifiedCoverage.Count == 0 ? null : Math.Round(verifiedCoverage.Average(), 4),
            Core = sufficient ? current.Count(m => m.Level == "core") : null,
            PreviousCore = days.Count(d => d.Eligible && d.Date <= asOf.AddDays(-window) && d.Date > asOf.AddDays(-2 * window)) >= 8
                ? previousPeriod.Count(m => m.Level == "core") : null,
            RegularIncludingCore = sufficient ? current.Count(m => m.Level is "core" or "regular") : null,
            FirstObserved = sufficient ? current.Count(m => m.FirstObserved > asOf.AddDays(-window)) : null,
            Declining = sufficient ? current.Count(m => m.Declining) : null,
            Distribution = sufficient ? new[] { "core", "regular", "returning", "occasional" }.ToDictionary(l => l, l => current.Count(m => m.Level == l)) : null,
            Trend = trend,
            Transitions = states.SelectMany(s => new[] { s.Item2.Transition, s.Item2.Returned && s.Item2.Transition != "returned" ? "returned" : null })
                .Where(t => t != null).GroupBy(t => t!)
                .ToDictionary(g => g.Key, g => g.Count())
        }, current, states);
    }

    public static DateTime FloorMinute(DateTime value) => new(value.Ticks - value.Ticks % TimeSpan.TicksPerMinute, DateTimeKind.Utc);
    private static DateOnly WeekStart(DateOnly value) => value.AddDays(-((7 + (int)value.DayOfWeek - (int)DayOfWeek.Monday) % 7));
}
