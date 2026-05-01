using Microsoft.EntityFrameworkCore;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Domain.Entities;
using Npgsql;
using System.Text;

namespace MyStreamHistory.ViewerService.Infrastructure.Persistence;

public class ViewerStatsRepository : IViewerStatsRepository
{
    private readonly ViewerServiceDbContext _context;

    public ViewerStatsRepository(ViewerServiceDbContext context)
    {
        _context = context;
    }

    public async Task<ViewerStats?> GetByViewerAndStreamerAsync(Guid viewerId, string streamerTwitchUserId, CancellationToken cancellationToken = default)
    {
        return await _context.ViewerStats
            .FirstOrDefaultAsync(s => s.ViewerId == viewerId && s.StreamerTwitchUserId == streamerTwitchUserId, cancellationToken);
    }

    public async Task<List<ViewerStats>> GetByViewerIdsAndStreamerAsync(List<Guid> viewerIds, string streamerTwitchUserId, CancellationToken cancellationToken = default)
    {
        var distinctViewerIds = viewerIds.Distinct().ToList();
        if (!distinctViewerIds.Any())
        {
            return new List<ViewerStats>();
        }

        return await _context.ViewerStats
            .Where(s => s.StreamerTwitchUserId == streamerTwitchUserId && distinctViewerIds.Contains(s.ViewerId))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ViewerStats>> GetTopViewersByStreamerAsync(string streamerTwitchUserId, int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _context.ViewerStats
            .Include(s => s.Viewer)
            .Where(s => s.StreamerTwitchUserId == streamerTwitchUserId)
            .OrderByDescending(s => s.Experience)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ViewerStats>> GetByViewerIdAsync(Guid viewerId, CancellationToken cancellationToken = default)
    {
        return await _context.ViewerStats
            .Where(s => s.ViewerId == viewerId)
            .ToListAsync(cancellationToken);
    }

    public async Task BulkUpsertAsync(List<ViewerStats> stats, CancellationToken cancellationToken = default)
    {
        var upserts = stats
            .GroupBy(s => new { s.ViewerId, s.StreamerTwitchUserId })
            .Select(g => g.Last())
            .ToList();

        foreach (var batch in upserts.Chunk(1000))
        {
            await ExecuteUpsertAsync(batch, incrementExisting: false, cancellationToken);
        }
    }

    public async Task BulkIncrementAsync(List<ViewerStats> statDeltas, CancellationToken cancellationToken = default)
    {
        var increments = statDeltas
            .GroupBy(s => new { s.ViewerId, s.StreamerTwitchUserId })
            .Select(g =>
            {
                var last = g.Last();
                var minutesWatched = g.Sum(s => s.MinutesWatched);
                var earnedMsgPoints = g.Sum(s => s.EarnedMsgPoints);

                return new ViewerStats
                {
                    Id = last.Id,
                    ViewerId = last.ViewerId,
                    StreamerTwitchUserId = last.StreamerTwitchUserId,
                    MinutesWatched = minutesWatched,
                    EarnedMsgPoints = earnedMsgPoints,
                    Experience = Math.Round(earnedMsgPoints / 4 + (decimal)minutesWatched / 30, 2),
                    LastUpdatedAt = last.LastUpdatedAt
                };
            })
            .ToList();

        foreach (var batch in increments.Chunk(1000))
        {
            await ExecuteUpsertAsync(batch, incrementExisting: true, cancellationToken);
        }
    }

    private async Task ExecuteUpsertAsync(IReadOnlyList<ViewerStats> stats, bool incrementExisting, CancellationToken cancellationToken)
    {
        if (!stats.Any())
        {
            return;
        }

        var sql = new StringBuilder();
        var parameters = new List<NpgsqlParameter>(stats.Count * 7);

        sql.AppendLine("""
            INSERT INTO "ViewerStats"
                ("Id", "ViewerId", "StreamerTwitchUserId", "MinutesWatched", "EarnedMsgPoints", "Experience", "LastUpdatedAt")
            VALUES
            """);

        for (var i = 0; i < stats.Count; i++)
        {
            if (i > 0)
            {
                sql.AppendLine(",");
            }

            sql.Append($"(@id{i}, @viewerId{i}, @streamerTwitchUserId{i}, @minutesWatched{i}, @earnedMsgPoints{i}, @experience{i}, @lastUpdatedAt{i})");

            var stat = stats[i];
            parameters.Add(new NpgsqlParameter($"id{i}", stat.Id));
            parameters.Add(new NpgsqlParameter($"viewerId{i}", stat.ViewerId));
            parameters.Add(new NpgsqlParameter($"streamerTwitchUserId{i}", stat.StreamerTwitchUserId));
            parameters.Add(new NpgsqlParameter($"minutesWatched{i}", stat.MinutesWatched));
            parameters.Add(new NpgsqlParameter($"earnedMsgPoints{i}", stat.EarnedMsgPoints));
            parameters.Add(new NpgsqlParameter($"experience{i}", stat.Experience));
            parameters.Add(new NpgsqlParameter($"lastUpdatedAt{i}", stat.LastUpdatedAt));
        }

        if (incrementExisting)
        {
            sql.AppendLine("""

                ON CONFLICT ("ViewerId", "StreamerTwitchUserId") DO UPDATE SET
                    "MinutesWatched" = "ViewerStats"."MinutesWatched" + EXCLUDED."MinutesWatched",
                    "EarnedMsgPoints" = "ViewerStats"."EarnedMsgPoints" + EXCLUDED."EarnedMsgPoints",
                    "Experience" = ROUND(
                        ("ViewerStats"."EarnedMsgPoints" + EXCLUDED."EarnedMsgPoints") / 4
                        + ("ViewerStats"."MinutesWatched" + EXCLUDED."MinutesWatched")::numeric / 30,
                        2),
                    "LastUpdatedAt" = EXCLUDED."LastUpdatedAt";
                """);
        }
        else
        {
            sql.AppendLine("""

                ON CONFLICT ("ViewerId", "StreamerTwitchUserId") DO UPDATE SET
                    "MinutesWatched" = EXCLUDED."MinutesWatched",
                    "EarnedMsgPoints" = EXCLUDED."EarnedMsgPoints",
                    "Experience" = EXCLUDED."Experience",
                    "LastUpdatedAt" = EXCLUDED."LastUpdatedAt";
                """);
        }

        await _context.Database.ExecuteSqlRawAsync(sql.ToString(), parameters, cancellationToken);
    }
}

