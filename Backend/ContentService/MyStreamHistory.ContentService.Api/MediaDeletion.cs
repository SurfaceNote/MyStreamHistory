using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace MyStreamHistory.ContentService.Api;

public enum MediaDeleteResult { Deleted, NotFound, InUse }

public sealed class MediaDeletion(ContentDbContext db, MediaStorage storage)
{
    public async Task<MediaDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginMediaMutationAsync(cancellationToken);
        var asset = await db.Media.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (asset is null) return MediaDeleteResult.NotFound;
        if (await db.PublishedMedia.AnyAsync(x => x.MediaId == id, cancellationToken)) return MediaDeleteResult.InUse;
        var articles = await db.Articles.AsNoTracking().ToListAsync(cancellationToken);
        if (articles.Any(article => References(article, id))) return MediaDeleteResult.InUse;

        // Keep the database record if S3 rejects the deletion; retries are safe because DeleteObject is idempotent.
        await storage.DeleteAsync(asset, cancellationToken);
        db.Media.Remove(asset);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return MediaDeleteResult.Deleted;
    }

    public static bool References(Article article, Guid id) => article.CoverId == id || BodyReferences(article.Body, id)
        || (article.PublishedAt is not null && (article.PublishedCoverId == id
            || (article.PublishedBody is not null && BodyReferences(article.PublishedBody, id))));

    private static bool BodyReferences(string body, Guid id)
    {
        using var document = JsonDocument.Parse(body);
        return ContentDocument.Validate(document.RootElement).Contains(id);
    }
}
