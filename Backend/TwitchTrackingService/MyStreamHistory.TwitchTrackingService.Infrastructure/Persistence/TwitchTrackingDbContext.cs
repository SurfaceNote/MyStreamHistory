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
    public DbSet<TwitchCategory> TwitchCategories => Set<TwitchCategory>();
    public DbSet<StreamCategory> StreamCategories => Set<StreamCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StreamSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TwitchUserId);
            entity.HasIndex(e => e.IsLive);
            entity.HasIndex(e => e.StreamId);
            
            entity.Property(e => e.StreamerLogin).HasMaxLength(100).IsRequired();
            entity.Property(e => e.StreamerDisplayName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.StreamerAvatarUrl).HasMaxLength(500);
            entity.Property(e => e.GameName).HasMaxLength(200);
            entity.Property(e => e.StreamId).HasMaxLength(100);
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

        modelBuilder.Entity<TwitchCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TwitchId).IsUnique();
            
            entity.Property(e => e.TwitchId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.BoxArtUrl).HasMaxLength(500).IsRequired();
            entity.Property(e => e.IgdbId).HasMaxLength(50);
        });

        modelBuilder.Entity<StreamCategory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StreamSessionId, e.StartedAt });
            entity.HasIndex(e => new { e.StreamSessionId, e.EndedAt });
            entity.HasIndex(e => e.TwitchCategoryId);
            
            entity.HasOne(e => e.StreamSession)
                .WithMany(s => s.StreamCategories)
                .HasForeignKey(e => e.StreamSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.TwitchCategory)
                .WithMany(c => c.StreamCategories)
                .HasForeignKey(e => e.TwitchCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

