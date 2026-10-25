using System.Reflection;
using System.Text.Json;
using Amazon.S3;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using MyStreamHistory.ContentService.Api;
using MyStreamHistory.ContentService.Api.Controllers;
using Xunit;

namespace MyStreamHistory.ContentService.Api.Tests;

public sealed class LocalizationTests
{
    [Fact]
    public async Task ListsAndLookupAreLanguageSpecificAndDefaultToEnglish()
    {
        await using var f = await Fixture.Create();
        var en = f.Article("en", "shared"); var ru = f.Article("ru", "shared");
        en.Publish(); ru.Publish(); f.Db.AddRange(en, ru); await f.Db.SaveChangesAsync();
        var english = Json(await f.Controller.ListPublished("news"));
        Assert.Equal("en", english.GetProperty("items")[0].GetProperty("Language").GetString());
        Assert.Equal(1, english.GetProperty("total").GetInt32());
        var russian = Json(await f.Controller.GetPublished("news", "shared", "ru"));
        Assert.Equal(ru.Id, russian.GetProperty("Id").GetGuid());
        Assert.IsType<NotFoundResult>(await f.Controller.GetPublished("news", "shared", "cs"));
        Assert.IsType<BadRequestObjectResult>(await f.Controller.ListPublished("news", language: "xx"));
    }

    [Fact]
    public async Task PublicAlternativesContainOnlyPublishedVersionsOfTheSameGroup()
    {
        await using var f = await Fixture.Create();
        var en = f.Article("en", "original"); en.Publish();
        var ru = f.Article("ru", "perevod"); ru.TranslationGroupId = en.TranslationGroupId; ru.Publish();
        var de = f.Article("de", "entwurf"); de.TranslationGroupId = en.TranslationGroupId;
        var unrelated = f.Article("es", "independiente"); unrelated.Publish();
        f.Db.AddRange(en, ru, de, unrelated); await f.Db.SaveChangesAsync();
        var versions = Json(await f.Controller.GetPublished("news", "original")).GetProperty("Translations");
        Assert.Equal(2, versions.GetArrayLength());
        Assert.Contains(versions.EnumerateArray(), x => x.GetProperty("Path").GetString() == "/ru/news/perevod");
        Assert.DoesNotContain(versions.EnumerateArray(), x => x.GetProperty("Language").GetString() == "de");
        Assert.Equal(3, Json(await f.Controller.GetAdmin(en.Id)).GetProperty("Translations").GetArrayLength());
        await f.Controller.Unpublish(ru.Id, ru.Revision);
        Assert.Single(Json(await f.Controller.GetPublished("news", "original")).GetProperty("Translations").EnumerateArray());
        Assert.IsType<NotFoundResult>(await f.Controller.GetPublished("news", "perevod", "ru"));
    }

    [Fact]
    public async Task TranslationCopiesDraftIntoSameGroupAndIsNeverAutomaticallyPublished()
    {
        await using var f = await Fixture.Create();
        var source = f.Article("en", "source"); source.Publish(); source.Title = "Latest draft";
        f.Db.Add(source); await f.Db.SaveChangesAsync();
        var response = Assert.IsType<CreatedResult>(await f.Controller.CreateTranslation(source.Id, new("ru")));
        var result = JsonSerializer.SerializeToElement(response.Value);
        var translation = await f.Db.Articles.FindAsync(result.GetProperty("Id").GetGuid());
        Assert.Equal(source.TranslationGroupId, translation!.TranslationGroupId);
        Assert.Equal(source.Title, translation.Title); Assert.Equal(source.Body, translation.Body);
        Assert.Equal(source.CoverId, translation.CoverId); Assert.Equal("ru", translation.Language);
        Assert.Null(translation.PublishedAt); Assert.False(translation.SlugLocked);
        Assert.StartsWith("untitled-", translation.Slug);
        Assert.IsType<ConflictObjectResult>(await f.Controller.CreateTranslation(source.Id, new("ru")));
        Assert.IsType<BadRequestObjectResult>(await f.Controller.CreateTranslation(source.Id, new("xx")));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("cs")]
    [InlineData("de")]
    [InlineData("es")]
    public async Task TranslationsCanBePublishedIndependently(string language)
    {
        await using var f = await Fixture.Create();
        var a = f.Article(language, "article"); f.Db.Add(a); await f.Db.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await f.Controller.Publish(a.Id, a.Revision));
        Assert.IsType<OkObjectResult>(await f.Controller.GetPublished("news", "article", language));
    }

    [Fact]
    public async Task DatabaseRejectsDuplicateGroupLanguagesEvenWhenRequestsRace()
    {
        await using var f = await Fixture.Create();
        var first = f.Article("ru", "first"); var duplicate = f.Article("ru", "second");
        duplicate.TranslationGroupId = first.TranslationGroupId;
        f.Db.AddRange(first, duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task LanguageAndTranslatedTypeCannotBeChangedAndRevisionIsChecked()
    {
        await using var f = await Fixture.Create();
        var en = f.Article("en", "original"); var ru = f.Article("ru", "translated");
        ru.TranslationGroupId = en.TranslationGroupId; f.Db.AddRange(en, ru); await f.Db.SaveChangesAsync();
        using var body = JsonDocument.Parse(en.Body);
        var input = new ContentController.ArticleInput("news", en.Slug, en.Title, "", "", "", body.RootElement, en.CoverId, en.Revision, "ru");
        Assert.IsType<BadRequestObjectResult>(await f.Controller.Save(en.Id, input));
        Assert.IsType<BadRequestObjectResult>(await f.Controller.Save(en.Id, input with { Language = "en", Type = "review" }));
        Assert.IsType<ConflictObjectResult>(await f.Controller.Save(en.Id, input with { Language = "en", Revision = 99 }));
    }

    [Fact]
    public async Task SitemapContainsLanguagePathsAndExcludesDrafts()
    {
        await using var f = await Fixture.Create();
        var ru = f.Article("ru", "published"); ru.Publish(); f.Db.AddRange(ru, f.Article("de", "private-draft")); await f.Db.SaveChangesAsync();
        var xml = Assert.IsType<ContentResult>(await f.Controller.Sitemap()).Content!;
        Assert.Contains("https://mystreamhistory.com/ru/news/published", xml);
        foreach (var language in ContentLanguages.All) Assert.Contains("https://mystreamhistory.com" + ContentLanguages.Prefix(language) + "/reviews", xml);
        Assert.DoesNotContain("private-draft", xml);
        var filtered = Json(await f.Controller.ListAdmin(null, null, null, language: "ru"));
        Assert.Equal(1, filtered.GetProperty("total").GetInt32());
    }

    [Fact]
    public void PostgresMigrationBackfillsEveryLegacyGroupBeforeCreatingTheUniqueIndex()
    {
        using var db = new ContentDbContext(new DbContextOptionsBuilder<ContentDbContext>().UseNpgsql("Host=localhost;Database=design_time").Options);
        var sql = db.GetService<IMigrator>().GenerateScript();
        var backfill = sql.IndexOf("UPDATE \"Articles\" SET \"TranslationGroupId\" = \"Id\"", StringComparison.Ordinal);
        var unique = sql.IndexOf("CREATE UNIQUE INDEX \"IX_Articles_TranslationGroupId_Language\"", StringComparison.Ordinal);
        Assert.True(backfill >= 0 && unique > backfill);
        Assert.Contains("DEFAULT 'en'", sql);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SlugCanBeCorrectedAfterPublicationIncludingWhenUnpublished(bool unpublish)
    {
        await using var f = await Fixture.Create();
        var article = f.Article("ru", "tyop"); article.Publish();
        if (unpublish) article.Unpublish();
        f.Db.Add(article); await f.Db.SaveChangesAsync();
        var input = new ContentController.ArticleInput("news", "corrected", "Pending title", "", "", "",
            JsonSerializer.Deserialize<JsonElement>(article.Body), article.CoverId, article.Revision, "ru");
        Assert.IsType<OkObjectResult>(await f.Controller.Save(article.Id, input));
        Assert.Equal("corrected", article.Slug);
        Assert.Equal(input.Revision + 1, article.Revision);
        Assert.IsType<BadRequestObjectResult>(await f.Controller.Save(article.Id, input with { Type = "review", Revision = article.Revision }));
        Assert.IsType<NotFoundResult>(await f.Controller.GetPublished("news", "tyop", "ru"));
        if (!unpublish)
        {
            var published = Json(await f.Controller.GetPublished("news", "corrected", "ru"));
            Assert.Equal("Example", published.GetProperty("Title").GetString());
            var sitemap = Assert.IsType<ContentResult>(await f.Controller.Sitemap()).Content!;
            Assert.Contains("/ru/news/corrected", sitemap);
            Assert.DoesNotContain("/ru/news/tyop", sitemap);
        }
    }

    [Fact]
    public async Task SlugChangesRejectInvalidDuplicateAndStaleInputs()
    {
        await using var f = await Fixture.Create();
        var article = f.Article("en", "original"); article.Publish();
        f.Db.AddRange(article, f.Article("en", "occupied")); await f.Db.SaveChangesAsync();
        var input = new ContentController.ArticleInput("news", "corrected", article.Title, "", "", "",
            JsonSerializer.Deserialize<JsonElement>(article.Body), article.CoverId, article.Revision);
        Assert.IsType<ConflictObjectResult>(await f.Controller.Save(article.Id, input with { Revision = 0 }));
        Assert.IsType<BadRequestObjectResult>(await f.Controller.Save(article.Id, input with { Slug = "Invalid slug" }));
        Assert.IsType<ConflictObjectResult>(await f.Controller.Save(article.Id, input with { Slug = "occupied" }));
        f.Db.ChangeTracker.Clear();
        Assert.Equal("original", (await f.Db.Articles.FindAsync(article.Id))!.Slug);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletingOneVersionRemovesPublicationReferencesButKeepsTranslationsAndMedia(bool published)
    {
        await using var f = await Fixture.Create();
        var article = f.Article("en", "original");
        var translation = f.Article("ru", "translated"); translation.TranslationGroupId = article.TranslationGroupId;
        translation.Publish();
        if (published) article.Publish();
        f.Db.AddRange(article, translation);
        f.Db.PublishedMedia.Add(new() { ArticleId = translation.Id, MediaId = f.Cover.Id });
        if (published) f.Db.PublishedMedia.Add(new() { ArticleId = article.Id, MediaId = f.Cover.Id });
        await f.Db.SaveChangesAsync();
        Assert.IsType<NoContentResult>(await f.Controller.Delete(article.Id, article.Revision));
        f.Db.ChangeTracker.Clear();
        Assert.IsType<NotFoundResult>(await f.Controller.GetAdmin(article.Id));
        Assert.IsType<NotFoundResult>(await f.Controller.GetPublished("news", "original"));
        Assert.Equal(1, Json(await f.Controller.ListAdmin(null, null, null)).GetProperty("total").GetInt32());
        Assert.DoesNotContain("/news/original", Assert.IsType<ContentResult>(await f.Controller.Sitemap()).Content!);
        Assert.Single(Json(await f.Controller.GetPublished("news", "translated", "ru")).GetProperty("Translations").EnumerateArray());
        Assert.Single(await f.Db.Media.ToListAsync());
        var reference = Assert.Single(await f.Db.PublishedMedia.ToListAsync());
        Assert.Equal(translation.Id, reference.ArticleId);
        Assert.IsType<NotFoundResult>(await f.Controller.Delete(article.Id, article.Revision));
    }

    [Fact]
    public async Task DeleteRequiresCurrentRevisionAndContentAuthorPolicy()
    {
        await using var f = await Fixture.Create();
        var article = f.Article("en", "original"); f.Db.Add(article); await f.Db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await f.Controller.Delete(article.Id, article.Revision - 1));
        Assert.NotNull(await f.Db.Articles.FindAsync(article.Id));
        var policy = typeof(ContentController).GetMethod(nameof(ContentController.Delete))!
            .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        Assert.Equal("ContentAuthor", policy?.Policy);
    }

    [Fact]
    public async Task RestoreDraftRecoversLatestPublishedContentAfterAnEmptyDraftWasSaved()
    {
        await using var f = await Fixture.Create();
        var article = f.Article("en", "original");
        var translation = f.Article("ru", "translated"); translation.TranslationGroupId = article.TranslationGroupId;
        f.Db.AddRange(article, translation); await f.Db.SaveChangesAsync();
        Assert.IsType<OkObjectResult>(await f.Controller.Publish(article.Id, article.Revision));
        article.Title = "Latest publication"; article.Summary = "Published summary";
        article.SeoTitle = "Published SEO title"; article.SeoDescription = "Published SEO description";
        article.Body = """{"type":"doc","content":[{"type":"image","attrs":{"src":"/content/media/COVER"}},{"type":"youtube","attrs":{"videoId":"dQw4w9WgXcQ"}}]}""".Replace("COVER", f.Cover.Id.ToString());
        Assert.IsType<OkObjectResult>(await f.Controller.Publish(article.Id, article.Revision));
        var publishedAt = article.PublishedAt;
        var publishedBody = article.PublishedBody;
        var publishedResponse = Json(await f.Controller.GetPublished("news", "original"));
        var empty = new ContentController.ArticleInput("news", "corrected", "", "", "", "",
            JsonSerializer.Deserialize<JsonElement>("""{"type":"doc","content":[]}"""), null, article.Revision);
        Assert.IsType<OkObjectResult>(await f.Controller.Save(article.Id, empty));
        var revision = article.Revision;
        var restored = Json(await f.Controller.RestoreDraft(article.Id, revision));
        Assert.Equal(revision + 1, restored.GetProperty("Revision").GetInt32());
        f.Db.ChangeTracker.Clear();
        var saved = (await f.Db.Articles.FindAsync(article.Id))!;
        Assert.Equal("Latest publication", saved.Title);
        Assert.Equal("Published summary", saved.Summary);
        Assert.Equal("Published SEO title", saved.SeoTitle);
        Assert.Equal("Published SEO description", saved.SeoDescription);
        Assert.Equal(publishedBody, saved.Body);
        Assert.Equal(f.Cover.Id, saved.CoverId);
        Assert.Equal("corrected", saved.Slug);
        Assert.Equal(publishedAt, saved.PublishedAt);
        Assert.Equal(publishedBody, saved.PublishedBody);
        Assert.Equal(publishedResponse.GetProperty("Body").GetRawText(), Json(await f.Controller.GetPublished("news", "corrected")).GetProperty("Body").GetRawText());
        Assert.Equal("Example", (await f.Db.Articles.FindAsync(translation.Id))!.Title);
        Assert.Single(await f.Db.PublishedMedia.Where(x => x.ArticleId == article.Id).ToListAsync());
        Assert.IsType<ConflictObjectResult>(await f.Controller.Save(article.Id, empty));
    }

    [Fact]
    public async Task RestoreDraftRejectsStaleRevisionsMissingArticlesAndUnavailablePublications()
    {
        await using var f = await Fixture.Create();
        var article = f.Article("en", "original"); article.Publish(); article.Title = "Draft to keep";
        var draft = f.Article("ru", "draft");
        var unpublished = f.Article("de", "unpublished"); unpublished.Publish(); unpublished.Unpublish();
        f.Db.AddRange(article, draft, unpublished); await f.Db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await f.Controller.RestoreDraft(article.Id, article.Revision - 1));
        Assert.IsType<BadRequestObjectResult>(await f.Controller.RestoreDraft(draft.Id, draft.Revision));
        Assert.IsType<BadRequestObjectResult>(await f.Controller.RestoreDraft(unpublished.Id, unpublished.Revision));
        Assert.IsType<NotFoundResult>(await f.Controller.RestoreDraft(Guid.NewGuid(), 1));
        f.Db.ChangeTracker.Clear();
        Assert.Equal("Draft to keep", (await f.Db.Articles.FindAsync(article.Id))!.Title);
        var policy = typeof(ContentController).GetMethod(nameof(ContentController.RestoreDraft))!
            .GetCustomAttribute<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>();
        Assert.Equal("ContentAuthor", policy?.Policy);
    }

    private static JsonElement Json(IActionResult result) => JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(result).Value);
    private sealed class Fixture : IAsyncDisposable
    {
        public ContentDbContext Db { get; } = new(new DbContextOptionsBuilder<ContentDbContext>().UseSqlite("Data Source=:memory:").Options);
        public MediaAsset Cover { get; } = new() { ObjectKey = "test.webp", ContentType = "image/webp" };
        public ContentController Controller { get; }
        public Fixture()
        {
            var storage = new MediaStorage(DispatchProxy.Create<IAmazonS3, MediaTests.S3Stub>(), new ContentStorageOptions { Bucket = "tests" });
            Controller = new ContentController(Db, storage, new MediaDelivery(new()), new MediaDeletion(Db, storage), NullLogger<ContentController>.Instance)
                { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        }
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); await f.Db.Database.OpenConnectionAsync(); await f.Db.Database.EnsureCreatedAsync();
            f.Db.Add(f.Cover); await f.Db.SaveChangesAsync(); return f;
        }
        public Article Article(string language, string slug) => new() { Language = language, Slug = slug, Title = "Example", CoverId = Cover.Id,
            Body = "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"Example\"}]}]}" };
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
