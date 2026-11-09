using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Services;

/// <summary>
/// Rules that keep the game shelf filled without manual work: which playthroughs collect new
/// streams automatically and which streamed games are worth suggesting as a new playthrough.
/// </summary>
public static class PlaythroughRules
{
    /// <summary>A game is suggested once it was streamed on at least this many different streams.</summary>
    public const int SuggestionMinimumStreams = 2;

    /// <summary>
    /// Games last streamed more than a year ago are not suggested. A year keeps games the streamer
    /// played a while ago and may want to put on the shelf as finished.
    /// </summary>
    public static readonly TimeSpan SuggestionRecentWindow = TimeSpan.FromDays(365);

    // Twitch categories that are not games. Ids are stable; names cover categories saved without data from the API.
    private static readonly HashSet<string> NonGameCategoryIds = new(StringComparer.Ordinal)
    {
        "509658",     // Just Chatting
        "26936",      // Music
        "509660",     // Art
        "417752",     // Talk Shows & Podcasts
        "1469308723", // Software and Game Development
        "509670",     // Science & Technology
        "509663",     // Special Events
        "509659",     // ASMR
        "509673",     // Makers & Crafting
        "509672",     // Travel & Outdoors
        "509667",     // Food & Drink
        "518203",     // Sports
        "116747788",  // Pools, Hot Tubs, and Beaches
        "509671",     // Fitness & Health
        "509669",     // Beauty & Body Art
        "515214",     // Politics
        "272263131",  // Animals, Aquariums, and Zoos
        "498566",     // I'm Only Sleeping
        "66082",      // Games + Demos
    };

    private static readonly HashSet<string> NonGameCategoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Just Chatting", "Music", "Art", "Talk Shows & Podcasts", "Software and Game Development",
        "Science & Technology", "Special Events", "ASMR", "Makers & Crafting", "Travel & Outdoors",
        "Food & Drink", "Sports", "Pools, Hot Tubs, and Beaches", "Fitness & Health", "Beauty & Body Art",
        "Politics", "Animals, Aquariums, and Zoos", "I'm Only Sleeping", "Games + Demos", "IRL",
    };

    public static bool IsGame(TwitchCategory category) =>
        !NonGameCategoryIds.Contains(category.TwitchId) && !NonGameCategoryNames.Contains(category.Name.Trim());

    /// <summary>A finished game stops collecting new streams: replaying it later is a new playthrough.</summary>
    public static bool CollectsNewStreams(PlaythroughStatus status, bool autoAddNewStreams) =>
        autoAddNewStreams && status != PlaythroughStatus.Completed;

    /// <summary>
    /// Suggest a playthrough for a game the streamer keeps coming back to, when nothing on the shelf
    /// already tracks it. After a finished or dropped playthrough only the streams after it count.
    /// </summary>
    public static bool ShouldSuggest(
        TwitchCategory category,
        IReadOnlyCollection<StreamCategory> unassignedSegments,
        IReadOnlyCollection<Playthrough> gamePlaythroughs,
        DateTime now)
    {
        if (unassignedSegments.Count == 0 || !IsGame(category))
        {
            return false;
        }

        var tracked = gamePlaythroughs.Any(p =>
            p.Status is PlaythroughStatus.Playing or PlaythroughStatus.Planned
            || CollectsNewStreams(p.Status, p.AutoAddNewStreams));
        if (tracked)
        {
            return false;
        }

        var previousEnd = gamePlaythroughs
            .SelectMany(p => p.PlaythroughStreamCategories)
            .Select(link => link.StreamCategory?.StartedAt)
            .Where(startedAt => startedAt.HasValue)
            .Select(startedAt => startedAt!.Value)
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

        var candidates = unassignedSegments.Where(sc => sc.StartedAt > previousEnd).ToList();
        if (candidates.Count == 0 || candidates.Max(sc => sc.StartedAt) < now - SuggestionRecentWindow)
        {
            return false;
        }

        return candidates.Select(sc => sc.StreamSessionId).Distinct().Count() >= SuggestionMinimumStreams;
    }
}
