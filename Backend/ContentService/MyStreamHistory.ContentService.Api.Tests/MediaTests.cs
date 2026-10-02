using System.Reflection;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyStreamHistory.ContentService.Api;
using MyStreamHistory.ContentService.Api.Controllers;
using Xunit;

namespace MyStreamHistory.ContentService.Api.Tests;

public sealed class MediaTests
{
    [Fact]
    public async Task PublicResponsesUseCdnForCoverAndInlineImagesWithoutChangingStoredBody()
    {
        await using var fixture = await Fixture.Create();
        var article = new Article { Slug = "test", CoverId = fixture.Asset.Id, Body = ImageBody(fixture.Asset.Id) };
        article.Publish();
        fixture.Db.Articles.Add(article);
        await fixture.Db.SaveChangesAsync();

        var response = Assert.IsType<OkObjectResult>(await fixture.Controller.GetPublished("news", "test"));
        var json = JsonSerializer.SerializeToElement(response.Value);
        Assert.Equal(fixture.Url, json.GetProperty("CoverUrl").GetString());
        Assert.Equal(fixture.Url, json.GetProperty("Body").GetProperty("content")[0].GetProperty("attrs").GetProperty("src").GetString());
        Assert.Equal("Cover", json.GetProperty("Body").GetProperty("content")[0].GetProperty("attrs").GetProperty("alt").GetString());
        Assert.Equal(ImageBody(fixture.Asset.Id), article.Body);
        Assert.Equal(article.Body, article.PublishedBody);
        var list = Assert.IsType<OkObjectResult>(await fixture.Controller.ListPublished("news"));
        Assert.Equal(fixture.Url, JsonSerializer.SerializeToElement(list.Value).GetProperty("items")[0].GetProperty("CoverUrl").GetString());
    }

    [Fact]
    public async Task LegacyPublishedImageUrlRedirectsButDraftIsStillDenied()
    {
        await using var fixture = await Fixture.Create();
        Assert.IsType<NotFoundResult>(await fixture.Controller.GetMedia(fixture.Asset.Id, default));
        var article = new Article { CoverId = fixture.Asset.Id };
        article.Publish();
        fixture.Db.Articles.Add(article);
        fixture.Db.PublishedMedia.Add(new PublishedMedia { ArticleId = article.Id, MediaId = fixture.Asset.Id });
        await fixture.Db.SaveChangesAsync();
        var result = Assert.IsType<RedirectResult>(await fixture.Controller.GetMedia(fixture.Asset.Id, default));
        Assert.Equal(fixture.Url, result.Url);
        Assert.False(result.Permanent);
        Assert.Equal("public, max-age=300", fixture.Controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task DeleteUnusedMediaRemovesTheExactS3ObjectAndDatabaseRecord()
    {
        await using var fixture = await Fixture.Create();
        Assert.IsType<NoContentResult>(await fixture.Controller.DeleteMedia(fixture.Asset.Id, default));
        Assert.False(await fixture.Db.Media.AnyAsync());
        Assert.Equal(new[] { ("test-bucket", fixture.Asset.ObjectKey) }, fixture.S3.Deleted);
        Assert.IsType<NotFoundResult>(await fixture.Controller.DeleteMedia(fixture.Asset.Id, default));
        Assert.Single(fixture.S3.Deleted);
    }

    [Theory]
    [InlineData("draft-cover")]
    [InlineData("draft-body")]
    [InlineData("published-cover")]
    [InlineData("published-body")]
    [InlineData("published-relation")]
    public async Task DeleteRefusesImagesReferencedByAnyDraftOrPublishedSnapshot(string reference)
    {
        await using var fixture = await Fixture.Create();
        var article = new Article();
        switch (reference)
        {
            case "draft-cover": article.CoverId = fixture.Asset.Id; break;
            case "draft-body": article.Body = ImageBody(fixture.Asset.Id); break;
            case "published-cover": article.PublishedAt = DateTime.UtcNow; article.PublishedCoverId = fixture.Asset.Id; break;
            case "published-body": article.PublishedAt = DateTime.UtcNow; article.PublishedBody = ImageBody(fixture.Asset.Id); break;
            case "published-relation":
                article.Publish();
                fixture.Db.PublishedMedia.Add(new PublishedMedia { ArticleId = article.Id, MediaId = fixture.Asset.Id });
                break;
        }
        fixture.Db.Articles.Add(article);
        await fixture.Db.SaveChangesAsync();
        Assert.IsType<ConflictObjectResult>(await fixture.Controller.DeleteMedia(fixture.Asset.Id, default));
        Assert.True(await fixture.Db.Media.AnyAsync(x => x.Id == fixture.Asset.Id));
        Assert.Empty(fixture.S3.Deleted);
    }

    [Fact]
    public async Task S3FailureKeepsMediaInLibraryAndCanBeRetried()
    {
        await using var fixture = await Fixture.Create();
        fixture.S3.Failure = new AmazonS3Exception("Denied");
        var failure = Assert.IsType<ObjectResult>(await fixture.Controller.DeleteMedia(fixture.Asset.Id, default));
        Assert.Equal(502, failure.StatusCode);
        Assert.True(await fixture.Db.Media.AnyAsync());
        fixture.S3.Failure = null;
        Assert.IsType<NoContentResult>(await fixture.Controller.DeleteMedia(fixture.Asset.Id, default));
        Assert.False(await fixture.Db.Media.AnyAsync());
    }

    [Fact]
    public void LocalDeliveryKeepsApiUrlsAndCdnEscapesObjectKeys()
    {
        var asset = new MediaAsset { ObjectKey = "content/2026/10/a b.webp" };
        var local = new MediaDelivery(new ContentDeliveryOptions());
        Assert.Equal($"/content/media/{asset.Id}", local.Url(asset));
        var cdn = new MediaDelivery(new ContentDeliveryOptions { BaseUrl = "https://cdn.mystreamhistory.com/" });
        Assert.Equal("https://cdn.mystreamhistory.com/content/2026/10/a%20b.webp", cdn.Url(asset));
    }

    [Theory]
    [InlineData("http://cdn.mystreamhistory.com")]
    [InlineData("https://cdn.mystreamhistory.com/s3-db")]
    [InlineData("https://user:password@cdn.mystreamhistory.com")]
    [InlineData("https://cdn.mystreamhistory.com/?key=value")]
    public void CdnConfigurationRejectsIncorrectBaseUrls(string url) =>
        Assert.Throws<InvalidOperationException>(() => new ContentDeliveryOptions { BaseUrl = url }.Validate());

    private static string ImageBody(Guid id) => JsonSerializer.Serialize(new
    {
        type = "doc", content = new[] { new { type = "image", attrs = new { src = $"/content/media/{id}", alt = "Cover" } } }
    });

    private sealed class Fixture : IAsyncDisposable
    {
        public ContentDbContext Db { get; }
        public S3Stub S3 { get; }
        public MediaAsset Asset { get; } = new() { ObjectKey = "content/2026/10/test.webp", ContentType = "image/webp" };
        public string Url => "https://cdn.mystreamhistory.com/" + Asset.ObjectKey;
        public ContentController Controller { get; }

        private Fixture()
        {
            Db = new ContentDbContext(new DbContextOptionsBuilder<ContentDbContext>().UseSqlite("Data Source=:memory:").Options);
            var client = DispatchProxy.Create<IAmazonS3, S3Stub>();
            S3 = (S3Stub)client;
            var storage = new MediaStorage(client, new ContentStorageOptions { Bucket = "test-bucket" });
            Controller = new ContentController(Db, storage, new MediaDelivery(new ContentDeliveryOptions { BaseUrl = "https://cdn.mystreamhistory.com" }),
                new MediaDeletion(Db, storage), NullLogger<ContentController>.Instance)
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        }

        public static async Task<Fixture> Create()
        {
            var fixture = new Fixture();
            await fixture.Db.Database.OpenConnectionAsync();
            await fixture.Db.Database.EnsureCreatedAsync();
            fixture.Db.Media.Add(fixture.Asset);
            await fixture.Db.SaveChangesAsync();
            return fixture;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    public class S3Stub : DispatchProxy
    {
        public List<(string Bucket, string Key)> Deleted { get; } = [];
        public Exception? Failure { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name != "DeleteObjectAsync") throw new NotSupportedException(method?.Name);
            if (Failure is not null) return Task.FromException<DeleteObjectResponse>(Failure);
            Deleted.Add(((string)args![0]!, (string)args[1]!));
            return Task.FromResult(new DeleteObjectResponse());
        }
    }
}
