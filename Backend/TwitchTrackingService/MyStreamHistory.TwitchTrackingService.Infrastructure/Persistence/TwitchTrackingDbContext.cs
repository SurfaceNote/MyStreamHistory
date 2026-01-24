using Microsoft.EntityFrameworkCore;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence;

public class TwitchTrackingDbContext : DbContext
{
    public TwitchTrackingDbContext(DbContextOptions<TwitchTrackingDbContext> options) : base(options)
    {
    }

    public DbSet<StreamSession> StreamSessions => Set<StreamSession>();
    public DbSet<EventSubSubscription> EventSubSubscriptions => Set<EventSubSubscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StreamSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TwitchUserId);
            entity.HasIndex(e => e.IsLive);
            
            entity.Property(e => e.StreamerLogin).HasMaxLength(100).IsRequired();
            entity.Property(e => e.StreamerDisplayName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.GameName).HasMaxLength(200);
        });

        modelBuilder.Entity<EventSubSubscription>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TwitchUserId);
            entity.HasIndex(e => e.TwitchSubscriptionId).IsUnique();
            
            entity.Property(e => e.TwitchSubscriptionId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.EventType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        });
    }
}

