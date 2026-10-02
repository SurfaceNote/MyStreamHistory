using System.Text.Json;
using System.Text.Json.Nodes;

namespace MyStreamHistory.ContentService.Api;

public sealed class ContentDeliveryOptions
{
    public string BaseUrl { get; init; } = "";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl)) return;
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var url) || url.Scheme != "https"
            || !string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Query)
            || !string.IsNullOrEmpty(url.Fragment) || url.AbsolutePath != "/")
            throw new InvalidOperationException("ContentDelivery:BaseUrl must be an HTTPS domain without a path, query or credentials.");
    }
}

public sealed class MediaDelivery(ContentDeliveryOptions options)
{
    public bool Enabled => !string.IsNullOrWhiteSpace(options.BaseUrl);

    public string Url(MediaAsset asset) => Enabled
        ? options.BaseUrl.TrimEnd('/') + "/" + string.Join("/", asset.ObjectKey.Split('/').Select(Uri.EscapeDataString))
        : $"/content/media/{asset.Id}";

    public JsonElement Body(string json, IReadOnlyDictionary<Guid, string> urls)
    {
        var document = JsonNode.Parse(json)!;
        Rewrite(document);
        return JsonSerializer.SerializeToElement(document);

        void Rewrite(JsonNode node)
        {
            if (node["type"]?.GetValue<string>() == "image" && node["attrs"] is JsonObject attrs
                && attrs["src"]?.GetValue<string>() is string src
                && src.StartsWith("/content/media/", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(src["/content/media/".Length..], out var id) && urls.TryGetValue(id, out var url))
                attrs["src"] = url;
            if (node["content"] is JsonArray children)
                foreach (var child in children)
                    if (child is not null) Rewrite(child);
        }
    }
}
