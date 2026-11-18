using System.Globalization;
using System.Text.Json;

namespace MyStreamHistory.Gateway.Application.Commands.TwitchEventSub;

/// <summary>Raised when a Twitch EventSub payload lacks a field or carries a value of the wrong shape.</summary>
public sealed class EventSubPayloadException(string message) : Exception(message);

/// <summary>
/// Field readers for EventSub payloads that fail with <see cref="EventSubPayloadException"/>
/// naming the offending field instead of KeyNotFound/Format/NullReference exceptions.
/// </summary>
public static class EventSubJson
{
    public static JsonElement RequireObject(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object ||
            !parent.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Object)
        {
            throw new EventSubPayloadException($"Field '{name}' is missing or is not an object");
        }

        return value;
    }

    public static string RequireString(JsonElement parent, string name) =>
        OptionalString(parent, name) ?? throw new EventSubPayloadException($"Field '{name}' is missing or null");

    /// <summary>Returns the string value, or null when the field is absent or null. Other value kinds are an error.</summary>
    public static string? OptionalString(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Null => null,
            _ => throw new EventSubPayloadException($"Field '{name}' is not a string")
        };
    }

    /// <summary>Reads a Twitch user id, which EventSub sends as a numeric string.</summary>
    public static int RequireNumericId(JsonElement parent, string name)
    {
        var value = RequireString(parent, name);
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
        {
            throw new EventSubPayloadException($"Field '{name}' is not a positive numeric id");
        }

        return id;
    }

    /// <summary>Reads an RFC 3339 timestamp and returns it as a UTC <see cref="DateTime"/>.</summary>
    public static DateTime RequireUtcDateTime(JsonElement parent, string name)
    {
        var value = RequireString(parent, name);
        if (!ProcessEventSubCommandHandler.TryParseTimestamp(value, out var timestamp))
        {
            throw new EventSubPayloadException($"Field '{name}' is not a valid timestamp");
        }

        return timestamp.UtcDateTime;
    }
}
