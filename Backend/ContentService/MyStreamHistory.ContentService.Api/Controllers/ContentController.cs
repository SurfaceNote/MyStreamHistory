using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MyStreamHistory.ContentService.Api.Controllers;

[ApiController]
[Route("content")]
public sealed class ContentController(ContentDbContext db, MediaStorage storage, ILogger<ContentController> logger) : ControllerBase
{
    public sealed record ArticleInput(string Type, string Slug, string Title, string? Summary,
        string SeoTitle, string SeoDescription, JsonElement Body, Guid? CoverId, int Revision);

    [HttpGet]
    public async Task<IActionResult> ListPublished([FromQuery] string? type, [FromQuery] int page = 1)
    {
        if (type is not null && type is not ("news" or "review")) return BadRequest("Invalid type.");
        page = Math.Clamp(page, 1, 10000);
        var query = db.Articles.AsNoTracking().Where(x => x.PublishedAt != null);
        if (type is not null) query = query.Where(x => x.Type == type);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.PublishedAt).Skip((page - 1) * 12).Take(12)
            .Select(x => new { x.Id, x.Type, x.Slug, Title = x.PublishedTitle, Summary = x.PublishedSummary,
                CoverId = x.PublishedCoverId, x.PublishedAt }).ToListAsync();
        return Ok(new { items, total, page, pageSize = 12 });
    }

    [HttpGet("sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var articles = await db.Articles.AsNoTracking().Where(x => x.PublishedAt != null)
            .Select(x => new { x.Type, x.Slug }).ToListAsync();
        var urls = new[] { "/", "/news", "/reviews" }
            .Select(path => new XElement(ns + "url", new XElement(ns + "loc", "https://mystreamhistory.com" + path)))
            .Concat(articles.Select(article => new XElement(ns + "url",
                new XElement(ns + "loc", $"https://mystreamhistory.com/{(article.Type == "news" ? "news" : "reviews")}/{article.Slug}"))));
        var xml = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(ns + "urlset", urls));
        return Content(xml.ToString(), "application/xml");
    }

    [HttpGet("{type}/{slug}")]
    public async Task<IActionResult> GetPublished(string type, string slug)
    {
        if (type is not ("news" or "review")) return NotFound();
        var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(x => x.Type == type && x.Slug == slug && x.PublishedAt != null);
        return article is null ? NotFound() : Ok(PublicResponse(article));
    }

    [HttpGet("admin/articles")]
    [Authorize(Policy = "ContentAuthor")]
    public async Task<IActionResult> ListAdmin([FromQuery] string? type, [FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1)
    {
        page = Math.Clamp(page, 1, 10000);
        var query = db.Articles.AsNoTracking().AsQueryable();
        if (type is "news" or "review") query = query.Where(x => x.Type == type);
        if (status == "published") query = query.Where(x => x.PublishedAt != null);
        if (status == "draft") query = query.Where(x => x.PublishedAt == null);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Title.ToLower().Contains(search.Trim().ToLower()));
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.UpdatedAt).Skip((page - 1) * 20).Take(20)
            .Select(x => new { x.Id, x.Type, x.Slug, x.Title, x.Summary, x.CoverId, x.PublishedAt, x.UpdatedAt, x.Revision }).ToListAsync();
        return Ok(new { items, total, page, pageSize = 20 });
    }

    [HttpPost("admin/articles")]
    [Authorize(Policy = "ContentAuthor")]
    public async Task<IActionResult> Create([FromBody] ArticleInput input)
    {
        if (input.Type is not ("news" or "review")) return BadRequest("Invalid article type.");
        var error = await ValidateInput(input);
        if (error is not null) return BadRequest(error);
        var article = new Article { Type = input.Type };
        CopyDraft(article, input);
        db.Articles.Add(article);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict("Article URL is already in use."); }
        logger.LogInformation("Content author created article {ArticleId}", article.Id);
        return Created($"/content/admin/articles/{article.Id}", AdminResponse(article));
    }

    [HttpGet("admin/articles/{id:guid}")]
    [Authorize(Policy = "ContentAuthor")]
    public async Task<IActionResult> GetAdmin(Guid id)
    {
        var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return article is null ? NotFound() : Ok(AdminResponse(article));
    }

    [HttpPut("admin/articles/{id:guid}")]
    [Authorize(Policy = "ContentAuthor")]
    public async Task<IActionResult> Save(Guid id, [FromBody] ArticleInput input)
    {
        var article = await db.Articles.FirstOrDefaultAsync(x => x.Id == id);
        if (article is null) return NotFound();
        if (article.Revision != input.Revision) return Conflict("This article was changed in another tab. Reload before saving.");
        if (article.SlugLocked && (input.Slug != article.Slug || input.Type != article.Type))
            return BadRequest("Published article type and URL cannot be changed.");
        var error = await ValidateInput(input);
        if (error is not null) return BadRequest(error);
        CopyDraft(article, input);
        article.Revision++;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict("This article was changed in another tab."); }
        catch (DbUpdateException) { return Conflict("Article URL is already in use."); }
        return Ok(AdminResponse(article));
    }

    [HttpGet("admin/articles/{id:guid}/preview")]
    [Authorize(Policy = "ContentAuthor")]
    public async Task<IActionResult> Preview(Guid id)
    {
        var article = await db.Articles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return article is null ? NotFound() : Ok(AdminResponse(article));
    }

    [HttpPost("admin/articles/{id:guid}/publish")]
    [Authorize(Policy = "ContentAuthor")]
    public async Task<IActionResult> Publish(Guid id, [FromBody] int revision)
    {
        var article = await db.Articles.Include(x => x.PublishedMedia).FirstOrDefaultAsync(x => x.Id == id);
        if (article is null) return NotFound();
        if (revision != article.Revision) return Conflict("Save the current draft before publishing.");
        if (string.IsNullOrWhiteSpace(article.Title) || article.CoverId is null)
            return BadRequest("Title and cover image are required.");
        var body = JsonDocument.Parse(article.Body).RootElement;
        if (!body.TryGetProperty("content", out var blocks) || blocks.GetArrayLength() == 0)
            return BadRequest("Article body is required.");
        var mediaIds = ContentDocument.Validate(body);
        mediaIds.Add(article.CoverId.Value);
        if (await db.Media.CountAsync(x => mediaIds.Contains(x.Id)) != mediaIds.Count)
            return BadRequest("An image is missing from the media library.");
        article.Publish();
        var existingIds = article.PublishedMedia.Select(x => x.MediaId).ToHashSet();
        db.PublishedMedia.RemoveRange(article.PublishedMedia.Where(x => !mediaIds.Contains(x.MediaId)));
        foreach (var mediaId in mediaIds.Except(existingIds))
            db.PublishedMedia.Add(new PublishedMedia { ArticleId = id, MediaId = mediaId });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict("This article was changed in another tab."); }
        logger.LogInformation("Content author published article {ArticleId}", id);
        return Ok(AdminResponse(article));
    }

    [HttpPost("admin/articles/{id:guid}/unpublish")]
    [Authorize(Policy = "ContentAuthor")]
    public async Task<IActionResult> Unpublish(Guid id, [FromBody] int revision)
    {
        var article = await db.Articles.Include(x => x.PublishedMedia).FirstOrDefaultAsync(x => x.Id == id);
        if (article is null) return NotFound();
        if (article.Revision != revision) return Conflict("This article was changed in another tab.");
        article.Unpublish();
        db.PublishedMedia.RemoveRange(article.PublishedMedia);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict("This article was changed in another tab."); }
        logger.LogInformation("Content author unpublished article {ArticleId}", id);
        return Ok(AdminResponse(article));
    }

    [HttpGet("admin/media")]
    [Authorize(Policy = "ContentAuthor")]
    public async Task<IActionResult> ListMedia() => Ok(await db.Media.AsNoTracking().OrderByDescending(x => x.CreatedAt)
        .Take(100).Select(x => new { x.Id, x.FileName, x.ContentType, x.Size, x.CreatedAt }).ToListAsync());

    [HttpPost("admin/media")]
    [Authorize(Policy = "ContentAuthor")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return BadRequest("Choose an image.");
        await using var input = new MemoryStream();
        await file.CopyToAsync(input, cancellationToken);
        input.Position = 0;
        var kind = ImageInspector.Inspect(input.GetBuffer().AsSpan(0, (int)input.Length));
        if (kind is null) return BadRequest("Only JPEG, PNG, WebP and GIF images are supported.");
        var max = kind == "image/gif" ? 20L * 1024 * 1024 : 10L * 1024 * 1024;
        if (file.Length > max) return BadRequest("Image exceeds the size limit.");
        var extension = kind switch { "image/jpeg" => "jpg", "image/png" => "png", "image/webp" => "webp", _ => "gif" };
        var asset = new MediaAsset
        {
            ContentType = kind,
            FileName = Path.GetFileName(file.FileName)[..Math.Min(Path.GetFileName(file.FileName).Length, 200)],
            Size = file.Length,
            ObjectKey = $"content/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}.{extension}"
        };
        await storage.PutAsync(asset, input, cancellationToken);
        db.Media.Add(asset);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Content author uploaded media {MediaId} ({ContentType}, {Size} bytes)", asset.Id, asset.ContentType, asset.Size);
        return Created($"/content/media/{asset.Id}", new { asset.Id, asset.FileName, asset.ContentType, asset.Size, asset.CreatedAt });
    }

    [HttpGet("media/{id:guid}")]
    public async Task<IActionResult> GetMedia(Guid id, CancellationToken cancellationToken)
    {
        var asset = await db.Media.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (asset is null) return NotFound();
        var published = await db.PublishedMedia.AnyAsync(x => x.MediaId == id && x.Article.PublishedAt != null);
        if (!published && !(User.Identity?.IsAuthenticated == true && User.IsInRole("admin")
            && User.HasClaim("TwitchId", HttpContext.RequestServices.GetRequiredService<IConfiguration>()["ContentAuthor:TwitchId"] ?? "")))
            return NotFound();
        Response.Headers.CacheControl = published ? "public, max-age=300" : "private, no-store";
        Response.ContentType = asset.ContentType;
        using var objectResponse = await storage.OpenAsync(asset, cancellationToken);
        await objectResponse.ResponseStream.CopyToAsync(Response.Body, cancellationToken);
        return new EmptyResult();
    }

    private async Task<string?> ValidateInput(ArticleInput input)
    {
        if (input.Type is not ("news" or "review") || string.IsNullOrWhiteSpace(input.Slug)
            || !Regex.IsMatch(input.Slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$") || input.Slug.Length > 100)
            return "Use a lowercase URL slug with letters, numbers and hyphens.";
        if (input.Title is null || input.SeoTitle is null || input.SeoDescription is null)
            return "Article metadata is required.";
        if (input.Title.Length > 180 || input.Summary?.Length > 500 || input.SeoTitle.Length > 180 || input.SeoDescription.Length > 300)
            return "Article metadata is too long.";
        HashSet<Guid> images;
        try { images = ContentDocument.Validate(input.Body); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or JsonException) { return exception.Message; }
        if (input.CoverId is Guid cover) images.Add(cover);
        return await db.Media.CountAsync(x => images.Contains(x.Id)) == images.Count ? null : "An image is missing from the media library.";
    }

    private static void CopyDraft(Article article, ArticleInput input)
    {
        article.Type = input.Type;
        article.Slug = input.Slug;
        article.Title = input.Title.Trim();
        article.Summary = input.Summary?.Trim() ?? "";
        article.SeoTitle = input.SeoTitle.Trim();
        article.SeoDescription = input.SeoDescription.Trim();
        article.Body = input.Body.GetRawText();
        article.CoverId = input.CoverId;
        article.UpdatedAt = DateTime.UtcNow;
    }

    private static object AdminResponse(Article a) => new
    {
        a.Id, a.Type, a.Slug, a.SlugLocked, a.Title, a.Summary, a.SeoTitle, a.SeoDescription,
        Body = JsonSerializer.Deserialize<JsonElement>(a.Body), a.CoverId, a.PublishedAt, a.UpdatedAt, a.Revision
    };

    private static object PublicResponse(Article a) => new
    {
        a.Id, a.Type, a.Slug, Title = a.PublishedTitle, Summary = a.PublishedSummary,
        SeoTitle = a.PublishedSeoTitle, SeoDescription = a.PublishedSeoDescription,
        Body = JsonSerializer.Deserialize<JsonElement>(a.PublishedBody!), CoverId = a.PublishedCoverId, a.PublishedAt
    };

}
