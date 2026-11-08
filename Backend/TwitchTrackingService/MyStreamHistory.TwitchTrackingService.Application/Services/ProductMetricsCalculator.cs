using MyStreamHistory.Shared.Base.Contracts.ProductActivity;

namespace MyStreamHistory.TwitchTrackingService.Application.Services;

public sealed record ProductSessionRow(int TwitchUserId, DateTime StartedAt, bool IsLive, string Login, string DisplayName, string? AvatarUrl);
public sealed record ProductSubscriptionRow(int TwitchUserId, string EventType, string Status);
public sealed record ProductActivityRow(int TwitchUserId, DateOnly Day, string Kind, DateTime FirstSeenAt, DateTime LastSeenAt);

/// <summary>
/// Builds the admin product overview. Subscriptions must come from the Twitch API (the source of truth);
/// pass null when Twitch could not be read, so recording state is reported as unknown instead of missing.
/// Definitions match docs/product-metrics.md:
/// weeks start on Monday (UTC); a channel is engaged in a week when it streamed and its owner
/// visited the site or its overlay/panel was used in the same week.
/// </summary>
public static class ProductMetricsCalculator
{
    private static readonly string[] RequiredEvents = ["stream.online", "stream.offline"];
    private static readonly HashSet<string> EngagementKinds =
        [ChannelActivityKinds.Visit, ChannelActivityKinds.Overlay, ChannelActivityKinds.Panel];

    public static GetProductOverviewResponse Build(
        IReadOnlyCollection<ProductSessionRow> sessions,
        IReadOnlyCollection<ProductSubscriptionRow>? subscriptions,
        IReadOnlyCollection<ProductActivityRow> activity,
        bool dataCollectionEnabled,
        DateTime now,
        int weeks)
    {
        weeks = Math.Clamp(weeks, 1, 52);
        var currentWeek = WeekStart(DateOnly.FromDateTime(now));
        var weekStarts = Enumerable.Range(0, weeks).Select(i => currentWeek.AddDays(-7 * (weeks - 1 - i))).ToList();

        var streamedByWeek = sessions
            .GroupBy(s => WeekStart(DateOnly.FromDateTime(s.StartedAt)))
            .ToDictionary(g => g.Key, g => g.Select(s => s.TwitchUserId).ToHashSet());
        var engagedByWeek = activity
            .Where(a => EngagementKinds.Contains(a.Kind))
            .GroupBy(a => WeekStart(a.Day))
            .ToDictionary(g => g.Key, g => g.Select(a => a.TwitchUserId).ToHashSet());

        var weekRows = weekStarts.Select(week =>
        {
            var streamed = streamedByWeek.GetValueOrDefault(week) ?? [];
            var engaged = engagedByWeek.GetValueOrDefault(week) ?? [];
            return new ProductWeekDto
            {
                WeekStart = week,
                ChannelsStreamed = streamed.Count,
                EngagedChannels = streamed.Count(engaged.Contains)
            };
        }).ToList();

        var sessionsByChannel = sessions.GroupBy(s => s.TwitchUserId).ToDictionary(g => g.Key, g => g.OrderBy(s => s.StartedAt).ToList());
        var activityByChannel = activity.GroupBy(a => a.TwitchUserId).ToDictionary(g => g.Key, g => g.ToList());
        var subscriptionsByChannel = (subscriptions ?? []).GroupBy(s => s.TwitchUserId).ToDictionary(g => g.Key, g => g.ToList());

        var registrations = activity
            .Where(a => a.Kind == ChannelActivityKinds.Registered)
            .GroupBy(a => a.TwitchUserId)
            .ToDictionary(g => g.Key, g => g.Min(a => a.FirstSeenAt));

        var firstWeek = weekStarts[0];
        var activation = registrations
            .Select(r => new { r.Key, RegisteredAt = r.Value, Week = WeekStart(DateOnly.FromDateTime(r.Value)) })
            .Where(r => r.Week >= firstWeek)
            .GroupBy(r => r.Week)
            .OrderBy(g => g.Key)
            .Select(g => new ActivationCohortDto
            {
                WeekStart = g.Key,
                Registered = g.Count(),
                StreamedWithin14Days = g.Count(r => FirstStreamWithin14Days(r.Key, r.RegisteredAt) != null),
                Activated = g.Count(r => FirstStreamWithin14Days(r.Key, r.RegisteredAt) is { } first
                    && activityByChannel.GetValueOrDefault(r.Key)?.Any(a => a.Kind == ChannelActivityKinds.Visit && a.LastSeenAt > first) == true)
            })
            .ToList();

        DateTime? FirstStreamWithin14Days(int channel, DateTime registeredAt) =>
            sessionsByChannel.GetValueOrDefault(channel)?
                .Where(s => s.StartedAt >= registeredAt && s.StartedAt < registeredAt.AddDays(14))
                .Select(s => (DateTime?)s.StartedAt)
                .FirstOrDefault();

        var since28Days = now.AddDays(-28);
        var channels = sessionsByChannel.Keys
            .Union(activityByChannel.Keys)
            .Union(subscriptionsByChannel.Keys)
            .Select(id =>
            {
                var channelSessions = sessionsByChannel.GetValueOrDefault(id) ?? [];
                var latest = channelSessions.LastOrDefault();
                var channelActivity = activityByChannel.GetValueOrDefault(id) ?? [];
                return new ProductChannelDto
                {
                    TwitchUserId = id,
                    Login = latest?.Login,
                    DisplayName = latest?.DisplayName,
                    AvatarUrl = latest?.AvatarUrl,
                    RegisteredAt = registrations.TryGetValue(id, out var registeredAt) ? registeredAt : null,
                    StreamsCount = channelSessions.Count,
                    StreamsLast28Days = channelSessions.Count(s => s.StartedAt >= since28Days),
                    LastStreamStartedAt = latest?.StartedAt,
                    IsLive = channelSessions.Any(s => s.IsLive),
                    Tracking = subscriptions == null && dataCollectionEnabled
                        ? "unknown"
                        : TrackingState(subscriptionsByChannel.GetValueOrDefault(id) ?? [], dataCollectionEnabled),
                    LastVisitAt = LastSeen(channelActivity, ChannelActivityKinds.Visit),
                    LastOverlayAt = LastSeen(channelActivity, ChannelActivityKinds.Overlay),
                    LastPanelAt = LastSeen(channelActivity, ChannelActivityKinds.Panel)
                };
            })
            .OrderByDescending(c => c.LastStreamStartedAt ?? DateTime.MinValue)
            .ThenBy(c => c.TwitchUserId)
            .ToList();

        return new GetProductOverviewResponse
        {
            GeneratedAt = now,
            ActivityTrackedSince = activity.Count == 0 ? null : activity.Min(a => a.Day),
            SubscriptionsAvailable = subscriptions != null,
            Weeks = weekRows,
            Activation = activation,
            Channels = channels
        };
    }

    public static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    /// <summary>Same states as the owner's profile panel: active, pending, missing, paused.</summary>
    public static string TrackingState(IReadOnlyCollection<ProductSubscriptionRow> subscriptions, bool dataCollectionEnabled)
    {
        if (!dataCollectionEnabled) return "paused";
        var byEvent = subscriptions.GroupBy(s => s.EventType)
            .ToDictionary(g => g.Key, g => g.Any(s => s.Status == "enabled") ? "enabled" : g.First().Status);
        if (RequiredEvents.Any(e => !byEvent.ContainsKey(e))) return "missing";
        if (RequiredEvents.All(e => byEvent[e] == "enabled")) return "active";
        if (RequiredEvents.Any(e => byEvent[e].Contains("pending", StringComparison.Ordinal))) return "pending";
        return "missing";
    }

    private static DateTime? LastSeen(IEnumerable<ProductActivityRow> rows, string kind) =>
        rows.Where(a => a.Kind == kind).Select(a => (DateTime?)a.LastSeenAt).Max();
}
