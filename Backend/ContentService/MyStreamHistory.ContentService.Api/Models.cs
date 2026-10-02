using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MyStreamHistory.ContentService.Api;

public sealed class Article
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "news";
    public string Slug { get; set; } = "";
    public bool SlugLocked { get; set; }
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string SeoTitle { get; set; } = "";
    public string SeoDescription { get; set; } = "";
    public string Body { get; set; } = "{\"type\":\"doc\",\"content\":[]}";
    public Guid? CoverId { get; set; }
    public string? PublishedTitle { get; set; }
    public string? PublishedSummary { get; set; }
    public string? PublishedSeoTitle { get; set; }
    public string? PublishedSeoDescription { get; set; }
    public string? PublishedBody { get; set; }
    public Guid? PublishedCoverId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int Revision { get; set; } = 1;
    public List<PublishedMedia> PublishedMedia { get; set; } = [];

    public void Publish()
    {
        PublishedTitle = Title;
        PublishedSummary = Summary;
        PublishedSeoTitle = SeoTitle;
        PublishedSeoDescription = SeoDescription;
        PublishedBody = Body;
        PublishedCoverId = CoverId;
        PublishedAt ??= DateTime.UtcNow;
        SlugLocked = true;
        UpdatedAt = DateTime.UtcNow;
        Revision++;
    }

    public void Unpublish()
    {
        PublishedAt = null;
        UpdatedAt = DateTime.UtcNow;
        Revision++;
    }
}

public sealed class MediaAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ObjectKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PublishedMedia
{
    public Guid ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    public Guid MediaId { get; set; }
    public MediaAsset Media { get; set; } = null!;
}

public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options) : DbContext(options)
{
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<MediaAsset> Media => Set<MediaAsset>();
    public DbSet<PublishedMedia> PublishedMedia => Set<PublishedMedia>();

    // Serialize reference changes and deletion across API replicas and the cleanup worker.
    public async Task<IDbContextTransaction> BeginMediaMutationAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (Database.IsNpgsql())
                await Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(739120, 1)", cancellationToken);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Article>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Type, x.Slug }).IsUnique();
            entity.HasIndex(x => x.PublishedAt);
            entity.Property(x => x.Body).HasColumnType("jsonb");
            entity.Property(x => x.PublishedBody).HasColumnType("jsonb");
            entity.Property(x => x.Revision).IsConcurrencyToken();
        });
        modelBuilder.Entity<MediaAsset>().HasKey(x => x.Id);
        modelBuilder.Entity<PublishedMedia>(entity =>
        {
            entity.HasKey(x => new { x.ArticleId, x.MediaId });
            entity.HasOne(x => x.Article).WithMany(x => x.PublishedMedia).HasForeignKey(x => x.ArticleId);
            entity.HasOne(x => x.Media).WithMany().HasForeignKey(x => x.MediaId);
        });
    }
}
