using Microsoft.EntityFrameworkCore;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Infrastructure.Persistence;

public class RankingExclusionRepository(ViewerServiceDbContext context) : IRankingExclusionRepository
{
    public Task<List<RankingExcludedAccount>> ListAsync(CancellationToken cancellationToken = default) =>
        context.RankingExcludedAccounts.AsNoTracking().OrderBy(a => a.Login).ToListAsync(cancellationToken);

    public async Task<HashSet<string>> GetLoginsAsync(CancellationToken cancellationToken = default) =>
        (await context.RankingExcludedAccounts.AsNoTracking().Select(a => a.Login).ToListAsync(cancellationToken))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public async Task<Dictionary<string, string>> GetDisplayNamesAsync(IReadOnlyCollection<string> logins, CancellationToken cancellationToken = default)
    {
        if (logins.Count == 0) return [];
        var rows = await context.Viewers.AsNoTracking()
            .Where(v => logins.Contains(v.Login.ToLower()))
            .Select(v => new { v.Login, v.DisplayName })
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(v => v.Login.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First().DisplayName, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> AddAsync(string login, string actor, CancellationToken cancellationToken = default)
    {
        if (await context.RankingExcludedAccounts.AnyAsync(a => a.Login == login, cancellationToken)) return false;
        context.RankingExcludedAccounts.Add(new RankingExcludedAccount { Login = login, AddedBy = actor, AddedAt = DateTime.UtcNow });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            // A concurrent request may have added the same login; anything else is a real failure.
            if (await context.RankingExcludedAccounts.AsNoTracking().AnyAsync(a => a.Login == login, cancellationToken)) return false;
            throw;
        }
    }

    public async Task<bool> RemoveAsync(string login, CancellationToken cancellationToken = default)
    {
        var account = await context.RankingExcludedAccounts.FirstOrDefaultAsync(a => a.Login == login, cancellationToken);
        if (account == null) return false;
        context.RankingExcludedAccounts.Remove(account);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<ChannelPresenceRow>> GetHighPresenceRowsAsync(int minChannelMinutes, double minShare, CancellationToken cancellationToken = default)
    {
        var excluded = context.RankingExcludedAccounts.Select(a => a.Login);
        // The longest presence in a channel approximates the channel's observed broadcast time.
        var channels = context.ViewerStats
            .GroupBy(s => s.StreamerTwitchUserId)
            .Select(g => new { StreamerTwitchUserId = g.Key, Observed = g.Max(s => s.MinutesWatched) })
            .Where(c => c.Observed >= minChannelMinutes);

        var rows = await (
                from stat in context.ViewerStats.AsNoTracking()
                join channel in channels on stat.StreamerTwitchUserId equals channel.StreamerTwitchUserId
                where stat.Viewer.TwitchUserId != stat.StreamerTwitchUserId
                      && !excluded.Contains(stat.Viewer.Login.ToLower())
                      && (double)stat.MinutesWatched >= channel.Observed * minShare
                select new
                {
                    stat.ViewerId,
                    stat.Viewer.TwitchUserId,
                    stat.Viewer.Login,
                    stat.Viewer.DisplayName,
                    stat.StreamerTwitchUserId,
                    stat.MinutesWatched,
                    channel.Observed,
                    stat.EarnedMsgPoints
                })
            .ToListAsync(cancellationToken);

        var viewerIds = rows.Select(r => r.ViewerId).Distinct().ToList();
        var channelCounts = await context.ViewerStats.AsNoTracking()
            .Where(s => viewerIds.Contains(s.ViewerId))
            .GroupBy(s => s.ViewerId)
            .Select(g => new { ViewerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.ViewerId, g => g.Count, cancellationToken);

        return rows.Select(r => new ChannelPresenceRow(
                r.TwitchUserId, r.Login, r.DisplayName, r.StreamerTwitchUserId,
                r.MinutesWatched, r.Observed, r.EarnedMsgPoints,
                channelCounts.GetValueOrDefault(r.ViewerId, 1)))
            .ToList();
    }
}
