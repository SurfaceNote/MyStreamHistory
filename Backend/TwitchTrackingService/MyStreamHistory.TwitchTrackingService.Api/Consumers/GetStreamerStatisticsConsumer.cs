using MassTransit;
using Microsoft.EntityFrameworkCore;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class GetStreamerStatisticsConsumer : IConsumer<GetStreamerStatisticsRequestContract>
{
    private readonly IStreamSessionRepository _streamSessionRepository;
    private readonly ITwitchCategoryRepository _twitchCategoryRepository;
    private readonly IStreamCategoryRepository _streamCategoryRepository;
    private readonly ILogger<GetStreamerStatisticsConsumer> _logger;

    public GetStreamerStatisticsConsumer(
        IStreamSessionRepository streamSessionRepository,
        ITwitchCategoryRepository twitchCategoryRepository,
        IStreamCategoryRepository streamCategoryRepository,
        ILogger<GetStreamerStatisticsConsumer> logger)
    {
        _streamSessionRepository = streamSessionRepository;
        _twitchCategoryRepository = twitchCategoryRepository;
        _streamCategoryRepository = streamCategoryRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetStreamerStatisticsRequestContract> context)
    {
        _logger.LogInformation("Received request to get streamer statistics for TwitchUserId {TwitchUserId}", 
            context.Message.TwitchUserId);

        try
        {
            // Get all stream sessions for the user
            var allSessions = await _streamSessionRepository.Query()
                .Where(s => s.TwitchUserId == context.Message.TwitchUserId)
                .Include(s => s.StreamCategories)
                    .ThenInclude(sc => sc.TwitchCategory)
                .ToListAsync(context.CancellationToken);

            var normalizedPeriod = NormalizePeriod(context.Message.Period);
            var (periodFrom, periodTo) = ResolvePeriod(normalizedPeriod, DateTime.UtcNow);
            var periodSessions = FilterSessionsByPeriod(allSessions, periodFrom, periodTo).ToList();

            // Calculate total streams count
            var totalStreamsCount = allSessions.Count;

            // Calculate total streamed hours
            var totalStreamedHours = allSessions
                .Where(s => s.EndedAt.HasValue)
                .Sum(s => (s.EndedAt!.Value - s.StartedAt).TotalHours);

            // Get all unique category IDs from stream categories
            var uniqueCategoryIds = allSessions
                .SelectMany(s => s.StreamCategories)
                .Select(sc => sc.TwitchCategoryId)
                .Distinct()
                .ToList();

            var totalUniqueGamesCount = uniqueCategoryIds.Count;

            // Get category details with time spent
            var categories = new List<CategoryStatisticsDto>();
            
            foreach (var categoryId in uniqueCategoryIds)
            {
                var category = await _twitchCategoryRepository.Query()
                    .FirstOrDefaultAsync(c => c.Id == categoryId, context.CancellationToken);

                if (category == null)
                    continue;

                // Calculate total hours for this category
                var categoryStreamCategories = await _streamCategoryRepository.Query()
                    .Where(sc => sc.TwitchCategoryId == categoryId)
                    .Include(sc => sc.StreamSession)
                    .Where(sc => sc.StreamSession.TwitchUserId == context.Message.TwitchUserId)
                    .ToListAsync(context.CancellationToken);

                var totalHours = categoryStreamCategories
                    .Where(sc => sc.EndedAt.HasValue)
                    .Sum(sc => (sc.EndedAt!.Value - sc.StartedAt).TotalHours);

                categories.Add(new CategoryStatisticsDto
                {
                    TwitchCategoryId = category.Id,
                    TwitchId = category.TwitchId,
                    Name = category.Name,
                    BoxArtUrl = category.BoxArtUrl,
                    IgdbId = category.IgdbId,
                    TotalHours = Math.Round(totalHours, 1)
                });
            }

            // Sort categories by total hours descending
            categories = categories.OrderByDescending(c => c.TotalHours).ToList();

            var response = new GetStreamerStatisticsResponseContract
            {
                TotalStreamsCount = totalStreamsCount,
                TotalUniqueGamesCount = totalUniqueGamesCount,
                TotalStreamedHours = Math.Round(totalStreamedHours, 1),
                AllGamesTotalHours = Math.Round(totalStreamedHours, 1),
                Categories = categories,
                Dashboard = BuildDashboard(normalizedPeriod, periodFrom, periodTo, periodSessions)
            };

            await context.RespondAsync(response);
            _logger.LogInformation("Successfully responded with statistics for TwitchUserId {TwitchUserId}", 
                context.Message.TwitchUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting streamer statistics for TwitchUserId {TwitchUserId}", 
                context.Message.TwitchUserId);
            
            await context.RespondAsync(new BaseFailedResponseContract
            {
                Reason = $"Error getting streamer statistics: {ex.Message}"
            });
        }
    }

    private static string NormalizePeriod(string? period)
    {
        return period?.ToLowerInvariant() switch
        {
            "7d" => "7d",
            "30d" => "30d",
            "90d" => "90d",
            "all" => "all",
            _ => "30d"
        };
    }

    private static (DateTime? From, DateTime To) ResolvePeriod(string period, DateTime now)
    {
        var today = now.Date;
        return period switch
        {
            "7d" => (today.AddDays(-6), now),
            "90d" => (today.AddDays(-89), now),
            "all" => (null, now),
            _ => (today.AddDays(-29), now)
        };
    }

    private static IEnumerable<StreamSession> FilterSessionsByPeriod(
        IEnumerable<StreamSession> sessions,
        DateTime? periodFrom,
        DateTime periodTo)
    {
        return sessions.Where(session =>
        {
            var sessionEnd = session.EndedAt ?? periodTo;
            return session.StartedAt <= periodTo && (!periodFrom.HasValue || sessionEnd >= periodFrom.Value);
        });
    }

    private static StreamerDashboardDto BuildDashboard(
        string period,
        DateTime? periodFrom,
        DateTime periodTo,
        List<StreamSession> sessions)
    {
        var firstSessionDate = sessions
            .OrderBy(s => s.StartedAt)
            .Select(s => (DateTime?)s.StartedAt.Date)
            .FirstOrDefault();

        var chartFrom = periodFrom ?? firstSessionDate ?? periodTo.Date;
        var streamedHoursByDay = BuildDailySeries(chartFrom, periodTo.Date, periodTo, sessions);

        return new StreamerDashboardDto
        {
            Period = period,
            From = chartFrom,
            To = periodTo,
            TotalStreamsCount = sessions.Count,
            TotalUniqueGamesCount = sessions.SelectMany(session => session.StreamCategories)
                .Select(segment => segment.TwitchCategoryId)
                .Distinct()
                .Count(),
            TotalStreamedHours = Math.Round(streamedHoursByDay.Sum(point => point.Value), 1),
            StreamedHoursByDay = streamedHoursByDay,
            StreamedHoursByWeek = BuildWeeklySeries(streamedHoursByDay),
            ChatPointsByDay = BuildEmptySeries(chartFrom, periodTo.Date),
            ViewerCountByDay = BuildViewerSeries(chartFrom, periodTo.Date, sessions),
            TopCategories = BuildTopCategories(sessions, chartFrom, periodTo)
        };
    }

    private static List<TimeSeriesPointDto> BuildDailySeries(
        DateTime from,
        DateTime to,
        DateTime periodTo,
        IEnumerable<StreamSession> sessions)
    {
        var values = EnumerateDays(from, to)
            .ToDictionary(day => day, _ => 0d);

        foreach (var session in sessions)
        {
            AddOverlapHours(values, session.StartedAt, session.EndedAt ?? periodTo, from, periodTo);
        }

        return values
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new TimeSeriesPointDto
            {
                Date = kvp.Key,
                Value = Math.Round(kvp.Value, 2)
            })
            .ToList();
    }

    private static List<TimeSeriesPointDto> BuildEmptySeries(DateTime from, DateTime to)
    {
        return EnumerateDays(from, to)
            .Select(day => new TimeSeriesPointDto { Date = day, Value = 0 })
            .ToList();
    }

    private static List<TimeSeriesPointDto> BuildViewerSeries(
        DateTime from,
        DateTime to,
        IEnumerable<StreamSession> sessions)
    {
        var values = EnumerateDays(from, to)
            .ToDictionary(day => day, _ => 0d);

        foreach (var dayGroup in sessions
            .Where(s => s.ViewerCount.HasValue)
            .GroupBy(s => s.StartedAt.Date))
        {
            if (values.ContainsKey(dayGroup.Key))
            {
                values[dayGroup.Key] = Math.Round(dayGroup.Average(s => s.ViewerCount!.Value), 1);
            }
        }

        return values
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new TimeSeriesPointDto
            {
                Date = kvp.Key,
                Value = kvp.Value
            })
            .ToList();
    }

    private static List<TimeSeriesPointDto> BuildWeeklySeries(List<TimeSeriesPointDto> dailySeries)
    {
        return dailySeries
            .GroupBy(point => GetWeekStart(point.Date))
            .OrderBy(group => group.Key)
            .Select(group => new TimeSeriesPointDto
            {
                Date = group.Key,
                Value = Math.Round(group.Sum(point => point.Value), 2)
            })
            .ToList();
    }

    private static List<CategoryAnalyticsDto> BuildTopCategories(
        List<StreamSession> sessions,
        DateTime periodFrom,
        DateTime periodTo)
    {
        return sessions
            .SelectMany(session => session.StreamCategories.Select(segment => new
            {
                Session = session,
                Segment = segment,
                Hours = CalculateOverlapHours(segment.StartedAt, segment.EndedAt ?? periodTo, periodFrom, periodTo)
            }))
            .Where(item => item.Hours > 0 && item.Segment.TwitchCategory != null)
            .GroupBy(item => item.Segment.TwitchCategoryId)
            .Select(group =>
            {
                var first = group.First();
                var category = first.Segment.TwitchCategory;

                return new CategoryAnalyticsDto
                {
                    TwitchCategoryId = category.Id,
                    TwitchId = category.TwitchId,
                    Name = category.Name,
                    BoxArtUrl = category.BoxArtUrl,
                    TotalHours = Math.Round(group.Sum(item => item.Hours), 1),
                    StreamsCount = group.Select(item => item.Session.Id).Distinct().Count(),
                    AverageViewers = Math.Round(group
                        .Where(item => item.Session.ViewerCount.HasValue)
                        .Select(item => item.Session.ViewerCount!.Value)
                        .DefaultIfEmpty(0)
                        .Average(), 1)
                };
            })
            .OrderByDescending(category => category.TotalHours)
            .Take(5)
            .ToList();
    }

    private static void AddOverlapHours(
        Dictionary<DateTime, double> values,
        DateTime startedAt,
        DateTime endedAt,
        DateTime periodFrom,
        DateTime periodToExclusive)
    {
        var current = startedAt > periodFrom ? startedAt : periodFrom;
        var end = endedAt < periodToExclusive ? endedAt : periodToExclusive;

        while (current < end)
        {
            var nextDay = current.Date.AddDays(1);
            var sliceEnd = nextDay < end ? nextDay : end;

            if (values.ContainsKey(current.Date))
            {
                values[current.Date] += (sliceEnd - current).TotalHours;
            }

            current = sliceEnd;
        }
    }

    private static double CalculateOverlapHours(DateTime startedAt, DateTime endedAt, DateTime periodFrom, DateTime periodTo)
    {
        var start = startedAt > periodFrom ? startedAt : periodFrom;
        var end = endedAt < periodTo ? endedAt : periodTo;
        return end <= start ? 0 : (end - start).TotalHours;
    }

    private static IEnumerable<DateTime> EnumerateDays(DateTime from, DateTime to)
    {
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            yield return day;
        }
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }
}

