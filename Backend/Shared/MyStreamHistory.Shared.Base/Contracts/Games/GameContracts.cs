namespace MyStreamHistory.Shared.Base.Contracts.Games;

/// <summary>Game page: statistics of one Twitch category across every streamer on the site.</summary>
public class GetGamePageRequest
{
    public string CategoryTwitchId { get; set; } = "";
}

public class GetGamePageResponse
{
    /// <summary>Null when nobody on the site has streamed the category.</summary>
    public GamePageDto? Game { get; set; }
}

public class GamePageDto
{
    public GameSummaryDto Summary { get; set; } = new();

    /// <summary>Live with the game first, then by the latest stream.</summary>
    public List<GameStreamerDto> Streamers { get; set; } = [];
}

public enum GamesCatalogFilter
{
    All = 0,
    /// <summary>Someone is streaming the game right now.</summary>
    Live = 1,
    /// <summary>At least one streamer has completed the game.</summary>
    Completed = 2
}

public enum GamesCatalogSort
{
    /// <summary>Most hours streamed in the last 30 days.</summary>
    Popular = 0,
    /// <summary>Streamed most recently.</summary>
    Recent = 1,
    /// <summary>Most hours streamed in total.</summary>
    Hours = 2
}

/// <summary>Games catalog: every game streamed on the site; non-game categories are left out.</summary>
public class GetGamesCatalogRequest
{
    /// <summary>Case-insensitive part of the game name.</summary>
    public string? Search { get; set; }

    /// <summary>ISO 639-1 code; keeps games that have a streamer whose main language it is.</summary>
    public string? Language { get; set; }

    public GamesCatalogFilter Filter { get; set; } = GamesCatalogFilter.All;
    public GamesCatalogSort Sort { get; set; } = GamesCatalogSort.Popular;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 48;
}

public class GetGamesCatalogResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }

    /// <summary>Games matching the request, across all pages.</summary>
    public int TotalCount { get; set; }

    /// <summary>Number of games in the whole catalog, before search and filters.</summary>
    public int CatalogCount { get; set; }

    /// <summary>Languages present in the whole catalog with their number of games, most common first.</summary>
    public List<GameLanguageCountDto> Languages { get; set; } = [];

    public List<GameSummaryDto> Items { get; set; } = [];
}

public class GameLanguageCountDto
{
    public string Language { get; set; } = "";
    public int GamesCount { get; set; }
}

public class GameSummaryDto
{
    public string TwitchId { get; set; } = "";
    public string Name { get; set; } = "";
    public string BoxArtUrl { get; set; } = "";
    public string? IgdbId { get; set; }

    /// <summary>False for Twitch categories that are not games (Just Chatting, Music, …).</summary>
    public bool IsGame { get; set; }

    public double TotalHours { get; set; }
    public double HoursLast30Days { get; set; }
    public int StreamsCount { get; set; }
    public int ChannelsCount { get; set; }

    /// <summary>Main languages of the game's streamers, most common first.</summary>
    public List<string> Languages { get; set; } = [];

    /// <summary>Streamers streaming the game right now.</summary>
    public int LiveCount { get; set; }

    /// <summary>Streamers who have completed the game.</summary>
    public int CompletedCount { get; set; }

    public DateTime FirstStreamedAt { get; set; }
    public DateTime LastStreamedAt { get; set; }
}

public class GameStreamerDto
{
    public int TwitchUserId { get; set; }
    public string StreamerLogin { get; set; } = "";
    public string StreamerDisplayName { get; set; } = "";
    public string? StreamerAvatarUrl { get; set; }

    /// <summary>Most common language of the streamer's streams with this game.</summary>
    public string? Language { get; set; }

    public double Hours { get; set; }
    public int StreamsCount { get; set; }
    public DateTime FirstStreamedAt { get; set; }
    public DateTime LastStreamedAt { get; set; }
    public bool IsLiveWithGame { get; set; }
    public GamePlaythroughDto? Playthrough { get; set; }
}

public class GamePlaythroughDto
{
    /// <summary>Playing, Planned, Dropped or Completed.</summary>
    public string Status { get; set; } = "";
    public string Title { get; set; } = "";
    public int StreamsCount { get; set; }
}

/// <summary>Asks for the game pages that search engines may index.</summary>
public class GetGameSitemapRequest
{
}

public class GetGameSitemapResponse
{
    public List<GameSitemapEntryDto> Games { get; set; } = new();
}

public class GameSitemapEntryDto
{
    public string TwitchId { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime LastStreamedAt { get; set; }
}
