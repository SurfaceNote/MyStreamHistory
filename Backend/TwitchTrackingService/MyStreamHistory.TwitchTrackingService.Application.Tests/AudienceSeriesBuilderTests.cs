using MyStreamHistory.TwitchTrackingService.Application.Services;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Application.Tests;

public class AudienceSeriesBuilderTests
{
    [Fact]
    public void BucketsUseStreamStartAndKeepGapsAndRealZeroes()
    {
        var start = new DateTime(2026, 10, 17, 12, 2, 0, DateTimeKind.Utc);
        ViewerCountSample Sample(int minute, int count) => new() { ObservedAt = start.AddMinutes(minute), ViewerCount = count };
        var points = AudienceSeriesBuilder.Build(start, start.AddMinutes(17),
            [Sample(-1, 999), Sample(0, 10), Sample(4, 20), Sample(10, 0), Sample(16, 40), Sample(17, 999)]);
        Assert.Equal(4, points.Count);
        Assert.Equal(15m, points[0].AverageViewers);
        Assert.Equal(2, points[0].SampleCount);
        Assert.Null(points[1].AverageViewers);
        Assert.Equal(0, points[1].SampleCount);
        Assert.Equal(0m, points[2].AverageViewers);
        Assert.Equal(start.AddMinutes(15), points[3].StartedAt);
    }

    [Fact]
    public void NoSamplesDoesNotReconstructAChartFromTheLastCount() =>
        Assert.Empty(AudienceSeriesBuilder.Build(DateTime.UtcNow, DateTime.UtcNow.AddHours(2), []));
}
