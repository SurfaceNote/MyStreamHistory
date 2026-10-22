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
