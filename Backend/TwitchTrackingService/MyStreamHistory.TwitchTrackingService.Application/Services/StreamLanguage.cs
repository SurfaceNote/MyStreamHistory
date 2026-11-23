namespace MyStreamHistory.TwitchTrackingService.Application.Services;

/// <summary>
/// Stream language as Twitch reports it in <c>language</c> / <c>broadcaster_language</c>:
/// an ISO 639-1 code such as "ru", or "other".
/// </summary>
public static class StreamLanguage
{
    public const int MaxLength = 10;

    /// <summary>Lower-cased trimmed code, or null when Twitch sent nothing usable.</summary>
    public static string? Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var normalized = language.Trim().ToLowerInvariant();
        return normalized.Length <= MaxLength ? normalized : null;
    }
}
