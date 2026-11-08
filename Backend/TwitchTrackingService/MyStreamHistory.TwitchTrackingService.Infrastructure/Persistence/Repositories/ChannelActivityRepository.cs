using Microsoft.EntityFrameworkCore;
using MyStreamHistory.Shared.Base.Contracts.ProductActivity;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Application.Services;

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Repositories;

public class ChannelActivityRepository(TwitchTrackingDbContext context) : IChannelActivityRepository
{
    public async Task RecordAsync(int twitchUserId, string kind, DateTime occurredAt, CancellationToken cancellationToken = default)
    {
        var utc = occurredAt.Kind == DateTimeKind.Utc ? occurredAt : DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc);
        var day = DateOnly.FromDateTime(utc);

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ChannelActivityDays" ("TwitchUserId", "Day", "Kind", "FirstSeenAt", "LastSeenAt")
            VALUES ({twitchUserId}, {day}, {kind}, {utc}, {utc})
            ON CONFLICT ("TwitchUserId", "Day", "Kind")
            DO UPDATE SET "LastSeenAt" = GREATEST("ChannelActivityDays"."LastSeenAt", EXCLUDED."LastSeenAt")
            """, cancellationToken);
    }

    public async Task<ProductData> GetProductDataAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await context.StreamSessions.AsNoTracking()
            .Select(s => new ProductSessionRow(s.TwitchUserId, s.StartedAt, s.IsLive, s.StreamerLogin, s.StreamerDisplayName, s.StreamerAvatarUrl))
            .ToListAsync(cancellationToken);
        var subscriptions = await context.EventSubSubscriptions.AsNoTracking()
            .Select(s => new ProductSubscriptionRow(s.TwitchUserId, s.EventType, s.Status))
            .ToListAsync(cancellationToken);
        var activity = await context.ChannelActivityDays.AsNoTracking()
            .Select(a => new ProductActivityRow(a.TwitchUserId, a.Day, a.Kind, a.FirstSeenAt, a.LastSeenAt))
            .ToListAsync(cancellationToken);
        return new ProductData(sessions, subscriptions, activity);
    }

    public async Task<ChannelCollectionSnapshot> GetCollectionSnapshotAsync(int twitchUserId, CancellationToken cancellationToken = default)
    {
        var subscriptions = await context.EventSubSubscriptions
            .AsNoTracking()
            .Where(s => s.TwitchUserId == twitchUserId)
            .OrderBy(s => s.EventType)
            .Select(s => new ChannelSubscriptionState(s.EventType, s.Status))
            .ToListAsync(cancellationToken);

        var sessions = context.StreamSessions.AsNoTracking().Where(s => s.TwitchUserId == twitchUserId);
        var streamsCount = await sessions.CountAsync(cancellationToken);
        var lastStream = await sessions
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new { s.StartedAt, s.EndedAt, s.IsLive })
            .FirstOrDefaultAsync(cancellationToken);

        var activity = context.ChannelActivityDays.AsNoTracking().Where(a => a.TwitchUserId == twitchUserId);
        var lastOverlay = await activity
            .Where(a => a.Kind == ChannelActivityKinds.Overlay)
            .MaxAsync(a => (DateTime?)a.LastSeenAt, cancellationToken);
        var lastPanel = await activity
            .Where(a => a.Kind == ChannelActivityKinds.Panel)
            .MaxAsync(a => (DateTime?)a.LastSeenAt, cancellationToken);

        return new ChannelCollectionSnapshot(
            subscriptions,
            streamsCount,
            lastStream?.IsLive ?? false,
            lastStream?.StartedAt,
            lastStream?.EndedAt,
            lastOverlay,
            lastPanel);
    }
}
