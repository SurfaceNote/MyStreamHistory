using Microsoft.EntityFrameworkCore;
using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Infrastructure.Persistence;

public class ViewerServiceDbContext : DbContext
{
    public ViewerServiceDbContext(DbContextOptions<ViewerServiceDbContext> options) : base(options)
    {
    }

    public DbSet<Viewer> Viewers => Set<Viewer>();
    public DbSet<ViewerCategoryWatch> ViewerCategoryWatches => Set<ViewerCategoryWatch>();
    public DbSet<ViewerStats> ViewerStats => Set<ViewerStats>();
    public DbSet<ProcessedEventSubMessage> ProcessedEventSubMessages => Set<ProcessedEventSubMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Viewer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TwitchUserId).IsUnique();
            
            entity.Property(e => e.TwitchUserId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Login).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ProfileImageUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.LastDataSyncAt);
        });

        modelBuilder.Entity<ViewerCategoryWatch>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ViewerId, e.StreamCategoryId }).IsUnique();
            entity.HasIndex(e => e.StreamCategoryId);
            entity.HasIndex(e => e.ViewerId);
            
            entity.Property(e => e.MinutesWatched).HasDefaultValue(0);
            entity.Property(e => e.ChatPoints).HasPrecision(18, 2).HasDefaultValue(0);
            entity.Property(e => e.Experience).HasPrecision(18, 2).HasDefaultValue(0);
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("now()");
            
            entity.HasOne(e => e.Viewer)
                .WithMany(v => v.CategoryWatches)
                .HasForeignKey(e => e.ViewerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ViewerStats>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ViewerId, e.StreamerTwitchUserId }).IsUnique();
            entity.HasIndex(e => e.StreamerTwitchUserId);
            entity.HasIndex(e => e.ViewerId);
            entity.HasIndex(e => new { e.StreamerTwitchUserId, e.Experience });
            
            entity.Property(e => e.StreamerTwitchUserId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.MinutesWatched).HasDefaultValue(0);
            entity.Property(e => e.EarnedMsgPoints).HasPrecision(18, 2).HasDefaultValue(0);
            entity.Property(e => e.Experience).HasPrecision(18, 2).HasDefaultValue(0);
            entity.Property(e => e.LastUpdatedAt).HasDefaultValueSql("now()");
            
            entity.HasOne(e => e.Viewer)
                .WithMany(v => v.Stats)
                .HasForeignKey(e => e.ViewerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProcessedEventSubMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MessageId).IsUnique();
            entity.HasIndex(e => e.ProcessedAt);
            
            entity.Property(e => e.MessageId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.EventType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ProcessedAt).HasDefaultValueSql("now()");
        });
    }
}

