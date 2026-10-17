using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Services;

public static class AudienceSeriesBuilder
{
    // Buckets are relative to the actual stream start. Missing buckets remain unknown.
    public static List<AudiencePointDto> Build(DateTime start, DateTime end, IEnumerable<ViewerCountSample> samples)
    {
        var groups = samples.Where(s => s.ObservedAt >= start && s.ObservedAt < end && s.ViewerCount >= 0)
            .GroupBy(s => (int)((s.ObservedAt - start).TotalMinutes / 5))
            .ToDictionary(g => g.Key, g => g.ToList());
        if (groups.Count == 0) return [];
        var points = new List<AudiencePointDto>();
        for (var i = 0; start.AddMinutes(i * 5) < end; i++)
        {
            var values = groups.GetValueOrDefault(i);
            points.Add(new() { StartedAt = start.AddMinutes(i * 5), SampleCount = values?.Count ?? 0,
                AverageViewers = values == null ? null : Math.Round(values.Average(v => (decimal)v.ViewerCount), 2) });
        }
        return points;
    }
}
