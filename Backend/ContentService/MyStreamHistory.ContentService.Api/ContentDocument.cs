using System.Text.Json;
using System.Text.RegularExpressions;

namespace MyStreamHistory.ContentService.Api;

public static partial class ContentDocument
{
    private static readonly HashSet<string> Nodes = ["doc", "paragraph", "heading", "bulletList", "orderedList", "listItem", "blockquote", "text", "image", "hardBreak"];
    private static readonly HashSet<string> Marks = ["bold", "italic", "link"];

    public static HashSet<Guid> Validate(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var type) || type.GetString() != "doc")
            throw new ArgumentException("Body must be a Tiptap document.");
        var images = new HashSet<Guid>();
        var count = 0;
        Walk(root, 0, images, ref count);
        return images;
    }

    private static void Walk(JsonElement node, int depth, HashSet<Guid> images, ref int count)
    {
        if (++count > 5000 || depth > 20) throw new ArgumentException("Document is too large.");
        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("type", out var typeProperty))
            throw new ArgumentException("Invalid document node.");
        var type = typeProperty.GetString();
        if (type is null || !Nodes.Contains(type)) throw new ArgumentException("Unsupported document node.");
        if (depth > 0 && type == "doc") throw new ArgumentException("Nested documents are not allowed.");
        foreach (var property in node.EnumerateObject())
            if (property.Name is not ("type" or "text" or "attrs" or "marks" or "content"))
                throw new ArgumentException("Unsupported document property.");

        if (type == "text")
        {
            if (!node.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String || text.GetString()!.Length > 100_000)
                throw new ArgumentException("Invalid text node.");
        }
        else if (node.TryGetProperty("text", out _)) throw new ArgumentException("Only text nodes can contain text.");
        if (node.TryGetProperty("attrs", out var attrs))
        {
            if (attrs.ValueKind != JsonValueKind.Object) throw new ArgumentException("Invalid node attributes.");
            if (type == "image")
            {
                if (!attrs.TryGetProperty("src", out var src) || src.ValueKind != JsonValueKind.String
                    || !Guid.TryParse(MediaPath().Match(src.GetString()!).Groups[1].Value, out var mediaId))
                    throw new ArgumentException("Images must use uploaded media.");
                images.Add(mediaId);
                foreach (var property in attrs.EnumerateObject())
                {
                    if (property.Name == "src") continue;
                    if (property.Name is "alt" or "title")
                    {
                        if (property.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.String)
                            || (property.Value.ValueKind == JsonValueKind.String && property.Value.GetString()!.Length > 300))
                            throw new ArgumentException("Invalid image attributes.");
                    }
                    else if (property.Name is "width" or "height")
                    {
                        if (property.Value.ValueKind != JsonValueKind.Null)
                            throw new ArgumentException("Image resizing is not supported.");
                    }
                    else throw new ArgumentException("Invalid image attributes.");
                }
            }
            else if (type == "heading")
            {
                if (!attrs.TryGetProperty("level", out var level) || level.GetInt32() is not (2 or 3))
                    throw new ArgumentException("Only H2 and H3 headings are allowed.");
            }
            else if (type == "orderedList")
            {
                foreach (var property in attrs.EnumerateObject())
                {
                    if (property.Name == "start")
                    {
                        if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt32(out var start)
                            || start is < 1 or > 10000)
                            throw new ArgumentException("Invalid ordered list start.");
                    }
                    else if (property.Name == "type")
                    {
                        if (property.Value.ValueKind != JsonValueKind.Null
                            && (property.Value.ValueKind != JsonValueKind.String
                                || property.Value.GetString() is not ("1" or "a" or "A" or "i" or "I")))
                            throw new ArgumentException("Invalid ordered list type.");
                    }
                    else throw new ArgumentException("Invalid ordered list attributes.");
                }
            }
            else if (attrs.EnumerateObject().Any()) throw new ArgumentException("Unsupported node attributes.");
        }
        if (node.TryGetProperty("marks", out var marks))
        {
            if (type != "text" || marks.ValueKind != JsonValueKind.Array) throw new ArgumentException("Invalid marks.");
            foreach (var mark in marks.EnumerateArray())
            {
                if (!mark.TryGetProperty("type", out var markType) || !Marks.Contains(markType.GetString() ?? ""))
                    throw new ArgumentException("Unsupported text mark.");
                if (markType.GetString() == "link")
                {
                    if (!mark.TryGetProperty("attrs", out var linkAttrs) || !linkAttrs.TryGetProperty("href", out var href)
                        || !Uri.TryCreate(href.GetString(), UriKind.Absolute, out var url)
                        || url.Scheme is not ("http" or "https"))
                        throw new ArgumentException("Links must use HTTP or HTTPS.");
                }
            }
        }
        if (node.TryGetProperty("content", out var content))
        {
            if (type is "text" or "image" or "hardBreak") throw new ArgumentException("Inline nodes cannot contain child nodes.");
            if (content.ValueKind != JsonValueKind.Array) throw new ArgumentException("Invalid node content.");
            foreach (var child in content.EnumerateArray())
            {
                if (child.ValueKind != JsonValueKind.Object || !child.TryGetProperty("type", out var childType)
                    || childType.ValueKind != JsonValueKind.String || !AllowedChild(type, childType.GetString()))
                    throw new ArgumentException("Invalid document structure.");
                Walk(child, depth + 1, images, ref count);
            }
        }
    }

    private static bool AllowedChild(string parent, string? child) => parent switch
    {
        "doc" or "blockquote" => child is "paragraph" or "heading" or "bulletList" or "orderedList" or "blockquote" or "image",
        "listItem" => child is "paragraph" or "heading" or "bulletList" or "orderedList" or "blockquote" or "image",
        "bulletList" or "orderedList" => child == "listItem",
        "paragraph" or "heading" => child is "text" or "hardBreak",
        _ => false
    };

    [GeneratedRegex("^/content/media/([0-9a-fA-F-]{36})$")]
    private static partial Regex MediaPath();
}
