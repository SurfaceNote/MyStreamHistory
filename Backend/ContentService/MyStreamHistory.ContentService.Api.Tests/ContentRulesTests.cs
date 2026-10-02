using System.Text.Json;
using MyStreamHistory.ContentService.Api;
using Xunit;

namespace MyStreamHistory.ContentService.Api.Tests;

public sealed class ContentRulesTests
{
    [Fact]
    public void YoutubeVideosCanBeSavedAndPublishedWithoutMediaReferences()
    {
        const string body = """{"type":"doc","content":[{"type":"youtube","attrs":{"videoId":"dQw4w9WgXcQ"}}]}""";
        using var document = JsonDocument.Parse(body);
        Assert.Empty(ContentDocument.Validate(document.RootElement));
        var article = new Article { Body = body };
        article.Publish();
        Assert.Equal(body, article.PublishedBody);
    }

    [Theory]
    [InlineData("""{"type":"youtube"}""")]
    [InlineData("""{"type":"youtube","attrs":{}}""")]
    [InlineData("""{"type":"youtube","attrs":{"videoId":null}}""")]
    [InlineData("""{"type":"youtube","attrs":{"videoId":123}}""")]
    [InlineData("""{"type":"youtube","attrs":{"videoId":"short"}}""")]
    [InlineData("""{"type":"youtube","attrs":{"videoId":"dQw4w9WgXcQ\n"}}""")]
    [InlineData("""{"type":"youtube","attrs":{"videoId":"https://evil.test"}}""")]
    [InlineData("""{"type":"youtube","attrs":{"videoId":"dQw4w9WgXcQ","src":"https://evil.test"}}""")]
    [InlineData("""{"type":"youtube","attrs":{"videoId":"dQw4w9WgXcQ"},"content":[]}""")]
    [InlineData("""{"type":"paragraph","content":[{"type":"youtube","attrs":{"videoId":"dQw4w9WgXcQ"}}]}""")]
    public void YoutubeVideosRejectInvalidIdsAttributesAndNesting(string node)
    {
        using var document = JsonDocument.Parse("{\"type\":\"doc\",\"content\":[" + node + "]}");
        Assert.Throws<ArgumentException>(() => ContentDocument.Validate(document.RootElement));
    }

    [Fact]
    public void PublishedSnapshotDoesNotChangeWhenDraftChanges()
    {
        var article = new Article { Title = "First", Body = "{\"type\":\"doc\",\"content\":[]}" };
        article.Publish();
        article.Title = "Edited draft";
        article.Body = "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\"}]}";
        Assert.Equal("First", article.PublishedTitle);
        Assert.Equal("{\"type\":\"doc\",\"content\":[]}", article.PublishedBody);
        Assert.NotNull(article.PublishedAt);
        Assert.True(article.SlugLocked);
        article.Unpublish();
        Assert.Null(article.PublishedAt);
    }

    [Fact]
    public void OnlyUploadedMediaAndHttpLinksAreAllowed()
    {
        var mediaId = Guid.NewGuid();
        using var valid = JsonDocument.Parse("{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"See more\",\"marks\":[{\"type\":\"link\",\"attrs\":{\"href\":\"https://example.com\"}}]}]},{\"type\":\"image\",\"attrs\":{\"src\":\"/content/media/" + mediaId + "\",\"alt\":\"Cover\"}}]}");
        Assert.Contains(mediaId, ContentDocument.Validate(valid.RootElement));
        using var unsafeLink = JsonDocument.Parse("{\"type\":\"doc\",\"content\":[{\"type\":\"text\",\"text\":\"x\",\"marks\":[{\"type\":\"link\",\"attrs\":{\"href\":\"javascript:alert(1)\"}}]}]}");
        Assert.Throws<ArgumentException>(() => ContentDocument.Validate(unsafeLink.RootElement));
        using var externalImage = JsonDocument.Parse("{\"type\":\"doc\",\"content\":[{\"type\":\"image\",\"attrs\":{\"src\":\"https://example.com/x.png\"}}]}");
        Assert.Throws<ArgumentException>(() => ContentDocument.Validate(externalImage.RootElement));
    }

    [Fact]
    public void TiptapImageAndOrderedListDefaultsCanBeSaved()
    {
        var mediaId = Guid.NewGuid();
        using var document = JsonDocument.Parse("{\"type\":\"doc\",\"content\":[{\"type\":\"orderedList\",\"attrs\":{\"start\":1,\"type\":null},\"content\":[{\"type\":\"listItem\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"First\"}]}]}]},{\"type\":\"image\",\"attrs\":{\"src\":\"/content/media/" + mediaId + "\",\"alt\":\"photo.gif\",\"title\":null,\"width\":null,\"height\":null}}]}");
        Assert.Contains(mediaId, ContentDocument.Validate(document.RootElement));
    }

    [Fact]
    public void DocumentRejectsInvalidNodeNesting()
    {
        using var document = JsonDocument.Parse("{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"doc\"}]}]}");
        Assert.Throws<ArgumentException>(() => ContentDocument.Validate(document.RootElement));
    }

    [Fact]
    public void DevelopmentCannotPointAtTimewebAndProductionRequiresIt()
    {
        var local = new ContentStorageOptions { Endpoint = "http://minio:9000", Region = "ru-1", Bucket = "local", AccessKey = "local", SecretKey = "local" };
        local.Validate(false);
        Assert.Throws<InvalidOperationException>(() => local.Validate(true));
        var timeweb = new ContentStorageOptions { Endpoint = "https://s3.twcstorage.ru", Region = "ru-1", Bucket = "private", AccessKey = "key", SecretKey = "secret" };
        timeweb.Validate(true);
        Assert.Throws<InvalidOperationException>(() => timeweb.Validate(false));
    }

    [Fact]
    public void ImageInspectorRejectsFakeHeadersAndOversizedDimensions()
    {
        Assert.Null(ImageInspector.Inspect(new byte[] { 0xff, 0xd8, 0xff, 0xd9 }));
        Assert.Equal("image/gif", ImageInspector.Inspect(Convert.FromBase64String("R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=")));
        Assert.Null(ImageInspector.Inspect(new byte[] { 71, 73, 70, 56, 57, 97, 0xff, 0xff, 0xff, 0xff, 0, 0, 0, 0 }));
    }
}
