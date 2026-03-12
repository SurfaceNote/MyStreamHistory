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
    public DbSet<Playthrough> Playthroughs => Set<Playthrough>();
    public DbSet<PlaythroughStreamCategory> PlaythroughStreamCategories => Set<PlaythroughStreamCategory>();

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

        modelBuilder.Entity<Playthrough>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TwitchUserId);
            entity.HasIndex(e => new { e.TwitchUserId, e.TwitchCategoryId });

            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(30)
                .IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.TwitchCategory)
                .WithMany(c => c.Playthroughs)
                .HasForeignKey(e => e.TwitchCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
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

        modelBuilder.Entity<PlaythroughStreamCategory>(entity =>
        {
            entity.HasKey(e => new { e.PlaythroughId, e.StreamCategoryId });
            entity.HasIndex(e => e.StreamCategoryId);

            entity.Property(e => e.AddedAt).HasDefaultValueSql("now()");

            entity.HasOne(e => e.Playthrough)
                .WithMany(p => p.PlaythroughStreamCategories)
                .HasForeignKey(e => e.PlaythroughId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.StreamCategory)
                .WithMany(sc => sc.PlaythroughStreamCategories)
                .HasForeignKey(e => e.StreamCategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

