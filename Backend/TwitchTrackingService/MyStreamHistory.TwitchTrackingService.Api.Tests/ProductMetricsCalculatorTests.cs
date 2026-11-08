using MyStreamHistory.Shared.Base.Contracts.ProductActivity;
using MyStreamHistory.TwitchTrackingService.Application.Services;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Api.Tests;

public class ProductMetricsCalculatorTests
{
    // Monday 2 November 2026 starts the current week; "now" is Wednesday.
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly ThisWeek = new(2026, 11, 2);

    [Fact]
    public void Weeks_CountEngagedChannelsOnlyWhenStreamAndUsageFallInTheSameWeek()
    {
        var sessions = new[]
        {
            Session(1, Now.AddDays(-1)),            // this week, owner visited this week
            Session(2, Now.AddDays(-2)),            // this week, overlay used this week
            Session(3, Now.AddDays(-1)),            // this week, owner visited only last week
            Session(1, Now.AddDays(-8)),            // last week, no usage that week
        };
        var activity = new[]
        {
            Activity(1, ChannelActivityKinds.Visit, Now.AddDays(-1)),
            Activity(2, ChannelActivityKinds.Overlay, Now.AddDays(-2)),
            Activity(3, ChannelActivityKinds.Visit, Now.AddDays(-9)),
            Activity(4, ChannelActivityKinds.Panel, Now),          // no stream: not counted
            Activity(1, ChannelActivityKinds.Registered, Now.AddDays(-1)), // registration is not usage
        };

        var result = ProductMetricsCalculator.Build(sessions, [], activity, true, Now, 3);

        Assert.Equal([ThisWeek.AddDays(-14), ThisWeek.AddDays(-7), ThisWeek], result.Weeks.Select(w => w.WeekStart));
        Assert.Equal(3, result.Weeks[2].ChannelsStreamed);
        Assert.Equal(2, result.Weeks[2].EngagedChannels);
        Assert.Equal(1, result.Weeks[1].ChannelsStreamed);
        Assert.Equal(0, result.Weeks[1].EngagedChannels);
        Assert.Equal(DateOnly.FromDateTime(Now.AddDays(-9)), result.ActivityTrackedSince);
    }

    [Fact]
    public void Activation_RequiresAStreamWithin14DaysAndAVisitAfterIt()
    {
        var registered = Now.AddDays(-20);
        var sessions = new[]
        {
            Session(1, registered.AddDays(3)),   // streamed, then visited
            Session(2, registered.AddDays(5)),   // streamed, visited only before the stream
            Session(3, registered.AddDays(20)),  // streamed too late
        };
        var activity = new[]
        {
            Activity(1, ChannelActivityKinds.Registered, registered),
            Activity(2, ChannelActivityKinds.Registered, registered),
            Activity(3, ChannelActivityKinds.Registered, registered),
            Activity(4, ChannelActivityKinds.Registered, registered),
            Activity(1, ChannelActivityKinds.Visit, registered.AddDays(4)),
            Activity(2, ChannelActivityKinds.Visit, registered.AddDays(1)),
            Activity(3, ChannelActivityKinds.Visit, registered.AddDays(21)),
        };

        var cohort = Assert.Single(ProductMetricsCalculator.Build(sessions, [], activity, true, Now, 12).Activation);

        Assert.Equal(ProductMetricsCalculator.WeekStart(DateOnly.FromDateTime(registered)), cohort.WeekStart);
        Assert.Equal(4, cohort.Registered);
        Assert.Equal(2, cohort.StreamedWithin14Days);
        Assert.Equal(1, cohort.Activated);
    }

    [Fact]
    public void Channels_ListEveryKnownChannelWithLatestNameTrackingAndSurfaces()
    {
        var sessions = new[]
        {
            Session(1, Now.AddDays(-40), login: "old_login"),
            Session(1, Now.AddDays(-1), login: "new_login", live: true),
            Session(2, Now.AddDays(-2)),
        };
        var subscriptions = new[]
        {
            new ProductSubscriptionRow(1, "stream.online", "enabled"),
            new ProductSubscriptionRow(1, "stream.offline", "enabled"),
            new ProductSubscriptionRow(2, "stream.online", "webhook_callback_verification_pending"),
            new ProductSubscriptionRow(2, "stream.offline", "enabled"),
            new ProductSubscriptionRow(3, "stream.online", "enabled"),
        };
        var activity = new[]
        {
            Activity(1, ChannelActivityKinds.Overlay, Now.AddHours(-2)),
            Activity(1, ChannelActivityKinds.Overlay, Now.AddDays(-3)),
            Activity(2, ChannelActivityKinds.Panel, Now.AddDays(-1)),
        };

        var channels = ProductMetricsCalculator.Build(sessions, subscriptions, activity, true, Now, 4).Channels;

        Assert.Equal([1, 2, 3], channels.Select(c => c.TwitchUserId));
        var first = channels[0];
        Assert.Equal("new_login", first.Login);
        Assert.True(first.IsLive);
        Assert.Equal(2, first.StreamsCount);
        Assert.Equal(1, first.StreamsLast28Days);
        Assert.Equal("active", first.Tracking);
        Assert.Equal(Now.AddHours(-2), first.LastOverlayAt);
        Assert.Null(first.LastVisitAt);
        Assert.Equal("pending", channels[1].Tracking);
        Assert.Equal(Now.AddDays(-1), channels[1].LastPanelAt);
        Assert.Equal("missing", channels[2].Tracking);
        Assert.Equal(0, channels[2].StreamsCount);
    }

    [Fact]
    public void Tracking_IsUnknownWhenTwitchCouldNotBeRead_NotMissing()
    {
        var sessions = new[] { Session(1, Now.AddDays(-1)) };

        var unavailable = ProductMetricsCalculator.Build(sessions, null, [], true, Now, 2);
        var paused = ProductMetricsCalculator.Build(sessions, null, [], false, Now, 2);

        Assert.False(unavailable.SubscriptionsAvailable);
        Assert.Equal("unknown", Assert.Single(unavailable.Channels).Tracking);
        Assert.Equal("paused", Assert.Single(paused.Channels).Tracking);
        Assert.True(ProductMetricsCalculator.Build(sessions, [], [], true, Now, 2).SubscriptionsAvailable);
    }

    [Fact]
    public void Tracking_IsPausedWhenCollectionIsDisabled()
    {
        var subscriptions = new[]
        {
            new ProductSubscriptionRow(1, "stream.online", "enabled"),
            new ProductSubscriptionRow(1, "stream.offline", "enabled"),
        };
        Assert.Equal("paused", ProductMetricsCalculator.TrackingState(subscriptions, false));
        Assert.Equal("active", ProductMetricsCalculator.TrackingState(subscriptions, true));
    }

    [Theory]
    [InlineData(2026, 11, 2, 2026, 11, 2)]
    [InlineData(2026, 11, 8, 2026, 11, 2)]
    [InlineData(2026, 11, 9, 2026, 11, 9)]
    public void Weeks_StartOnMonday(int y, int m, int d, int ey, int em, int ed) =>
        Assert.Equal(new DateOnly(ey, em, ed), ProductMetricsCalculator.WeekStart(new DateOnly(y, m, d)));

    private static ProductSessionRow Session(int channel, DateTime startedAt, string? login = null, bool live = false) =>
        new(channel, startedAt, live, login ?? $"channel{channel}", $"Channel {channel}", null);

    private static ProductActivityRow Activity(int channel, string kind, DateTime at) =>
        new(channel, DateOnly.FromDateTime(at), kind, at, at);
}
