using Microsoft.EntityFrameworkCore;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Domain.Entities;
using Npgsql;
using System.Text;

namespace MyStreamHistory.ViewerService.Infrastructure.Persistence;

public class ViewerCategoryWatchRepository : IViewerCategoryWatchRepository
{
    private readonly ViewerServiceDbContext _context;

    public ViewerCategoryWatchRepository(ViewerServiceDbContext context)
    {
        _context = context;
    }

    public async Task<ViewerCategoryWatch?> GetByViewerAndCategoryAsync(Guid viewerId, Guid streamCategoryId, CancellationToken cancellationToken = default)
    {
        return await _context.ViewerCategoryWatches
            .FirstOrDefaultAsync(w => w.ViewerId == viewerId && w.StreamCategoryId == streamCategoryId, cancellationToken);
    }

    public async Task<List<ViewerCategoryWatch>> GetByViewerIdsAndCategoryAsync(List<Guid> viewerIds, Guid streamCategoryId, CancellationToken cancellationToken = default)
    {
        var distinctViewerIds = viewerIds.Distinct().ToList();
        if (!distinctViewerIds.Any())
        {
            return new List<ViewerCategoryWatch>();
        }

        return await _context.ViewerCategoryWatches
            .Where(w => w.StreamCategoryId == streamCategoryId && distinctViewerIds.Contains(w.ViewerId))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ViewerCategoryWatch>> GetByViewerIdAsync(Guid viewerId, CancellationToken cancellationToken = default)
    {
        return await _context.ViewerCategoryWatches
            .Where(w => w.ViewerId == viewerId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ViewerCategoryWatch>> GetByStreamCategoryIdAsync(Guid streamCategoryId, CancellationToken cancellationToken = default)
    {
        return await _context.ViewerCategoryWatches
            .Include(w => w.Viewer)
            .Where(w => w.StreamCategoryId == streamCategoryId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ViewerCategoryWatch>> GetByStreamCategoryIdsAsync(List<Guid> streamCategoryIds, CancellationToken cancellationToken = default)
    {
        return await _context.ViewerCategoryWatches
            .Include(w => w.Viewer)
            .Where(w => streamCategoryIds.Contains(w.StreamCategoryId))
            .OrderByDescending(w => w.MinutesWatched)
            .ToListAsync(cancellationToken);
    }

    public async Task BulkUpsertAsync(List<ViewerCategoryWatch> watches, CancellationToken cancellationToken = default)
    {
        var upserts = watches
            .GroupBy(w => new { w.ViewerId, w.StreamCategoryId })
            .Select(g => g.Last())
            .ToList();

        foreach (var batch in upserts.Chunk(1000))
        {
            await ExecuteUpsertAsync(batch, incrementExisting: false, cancellationToken);
        }
    }

    public async Task BulkIncrementAsync(List<ViewerCategoryWatch> watchDeltas, CancellationToken cancellationToken = default)
    {
        var increments = watchDeltas
            .GroupBy(w => new { w.ViewerId, w.StreamCategoryId })
            .Select(g =>
            {
                var last = g.Last();
                var minutesWatched = g.Sum(w => w.MinutesWatched);
                var chatPoints = g.Sum(w => w.ChatPoints);

                return new ViewerCategoryWatch
                {
                    Id = last.Id,
                    ViewerId = last.ViewerId,
                    StreamCategoryId = last.StreamCategoryId,
                    MinutesWatched = minutesWatched,
                    ChatPoints = chatPoints,
                    Experience = Math.Round(chatPoints / 4 + (decimal)minutesWatched / 30, 2),
                    LastUpdatedAt = last.LastUpdatedAt
                };
            })
            .ToList();

        foreach (var batch in increments.Chunk(1000))
        {
            await ExecuteUpsertAsync(batch, incrementExisting: true, cancellationToken);
        }
    }

    private async Task ExecuteUpsertAsync(IReadOnlyList<ViewerCategoryWatch> watches, bool incrementExisting, CancellationToken cancellationToken)
    {
        if (!watches.Any())
        {
            return;
        }

        var sql = new StringBuilder();
        var parameters = new List<NpgsqlParameter>(watches.Count * 7);

        sql.AppendLine("""
            INSERT INTO "ViewerCategoryWatches"
                ("Id", "ViewerId", "StreamCategoryId", "MinutesWatched", "ChatPoints", "Experience", "LastUpdatedAt")
            VALUES
            """);

        for (var i = 0; i < watches.Count; i++)
        {
            if (i > 0)
            {
                sql.AppendLine(",");
            }

            sql.Append($"(@id{i}, @viewerId{i}, @streamCategoryId{i}, @minutesWatched{i}, @chatPoints{i}, @experience{i}, @lastUpdatedAt{i})");

            var watch = watches[i];
            parameters.Add(new NpgsqlParameter($"id{i}", watch.Id));
            parameters.Add(new NpgsqlParameter($"viewerId{i}", watch.ViewerId));
            parameters.Add(new NpgsqlParameter($"streamCategoryId{i}", watch.StreamCategoryId));
            parameters.Add(new NpgsqlParameter($"minutesWatched{i}", watch.MinutesWatched));
            parameters.Add(new NpgsqlParameter($"chatPoints{i}", watch.ChatPoints));
            parameters.Add(new NpgsqlParameter($"experience{i}", watch.Experience));
            parameters.Add(new NpgsqlParameter($"lastUpdatedAt{i}", watch.LastUpdatedAt));
        }

        if (incrementExisting)
        {
            sql.AppendLine("""

                ON CONFLICT ("ViewerId", "StreamCategoryId") DO UPDATE SET
                    "MinutesWatched" = "ViewerCategoryWatches"."MinutesWatched" + EXCLUDED."MinutesWatched",
                    "ChatPoints" = "ViewerCategoryWatches"."ChatPoints" + EXCLUDED."ChatPoints",
                    "Experience" = ROUND(
                        ("ViewerCategoryWatches"."ChatPoints" + EXCLUDED."ChatPoints") / 4
                        + ("ViewerCategoryWatches"."MinutesWatched" + EXCLUDED."MinutesWatched")::numeric / 30,
                        2),
                    "LastUpdatedAt" = EXCLUDED."LastUpdatedAt";
                """);
        }
        else
        {
            sql.AppendLine("""

                ON CONFLICT ("ViewerId", "StreamCategoryId") DO UPDATE SET
                    "MinutesWatched" = EXCLUDED."MinutesWatched",
                    "ChatPoints" = EXCLUDED."ChatPoints",
                    "Experience" = EXCLUDED."Experience",
                    "LastUpdatedAt" = EXCLUDED."LastUpdatedAt";
                """);
        }

        await _context.Database.ExecuteSqlRawAsync(sql.ToString(), parameters, cancellationToken);
    }
}

