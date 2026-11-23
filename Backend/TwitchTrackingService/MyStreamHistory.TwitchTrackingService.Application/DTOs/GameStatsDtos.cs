using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.DTOs;

/// <summary>One category segment of a stream, with the stream and category it belongs to.</summary>
public sealed record GameSegmentRow(
    Guid TwitchCategoryId,
    string CategoryTwitchId,
    string Name,
    string BoxArtUrl,
    string? IgdbId,
    Guid StreamSessionId,
    int TwitchUserId,
    string StreamerLogin,
    string StreamerDisplayName,
    string? StreamerAvatarUrl,
    string? Language,
    bool SessionIsLive,
    DateTime? SessionLastConfirmedLiveAt,
    DateTime SessionStartedAt,
    DateTime? SessionEndedAt,
    DateTime StartedAt,
    DateTime? EndedAt);

/// <summary>A streamer's playthrough of a game; <see cref="StreamsCount"/> counts distinct streams in it.</summary>
public sealed record GamePlaythroughRow(
    Guid TwitchCategoryId,
    int TwitchUserId,
    PlaythroughStatus Status,
    string Title,
    DateTime UpdatedAt,
    int StreamsCount);

/// <summary>Statistics of one game: the summary used by the catalog and the per-streamer rows of the game page.</summary>
public sealed record GameStats(GameSummary Summary, IReadOnlyList<GameStreamerStats> Streamers);

public sealed record GameSummary(
    Guid TwitchCategoryId,
    string TwitchId,
    string Name,
    string BoxArtUrl,
    string? IgdbId,
    double TotalHours,
    double HoursLast30Days,
    int StreamsCount,
    int ChannelsCount,
    // Main languages of the game's streamers, most common first.
    IReadOnlyList<string> Languages,
    int LiveCount,
    int CompletedCount,
    DateTime FirstStreamedAt,
    DateTime LastStreamedAt)
{
    /// <summary>Twitch categories without an IGDB id are not games (Just Chatting, Music, …).</summary>
    public bool IsGame => !string.IsNullOrEmpty(IgdbId);
}

public sealed record GameStreamerStats(
    int TwitchUserId,
    string StreamerLogin,
    string StreamerDisplayName,
    string? StreamerAvatarUrl,
    // Most common language of the streamer's streams with this game; null when none is known.
    string? Language,
    double Hours,
    int StreamsCount,
    DateTime FirstStreamedAt,
    DateTime LastStreamedAt,
    bool IsLiveWithGame,
    GamePlaythroughSummary? Playthrough);

public sealed record GamePlaythroughSummary(PlaythroughStatus Status, string Title, int StreamsCount);
