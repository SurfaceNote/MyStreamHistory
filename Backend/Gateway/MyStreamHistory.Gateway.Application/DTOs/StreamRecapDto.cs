namespace MyStreamHistory.Gateway.Application.DTOs;

/// <summary>
/// "How did the stream go": the stream compared with the channel's recent streams, and the people
/// in chat compared with who was there before. Chat accounts exclude the broadcaster and bots.
/// </summary>
public class StreamRecapDto
{
    public Guid StreamSessionId { get; set; }
    public bool IsLive { get; set; }
    public bool ViewerDataAvailable { get; set; }
    /// <summary>Recorded streams of the channel before this one. 0 means this is the first one.</summary>
    public int PreviousStreamsCount { get; set; }
    /// <summary>How many earlier streams the averages below are based on.</summary>
    public int ComparisonStreamsCount { get; set; }

    public int DurationMinutes { get; set; }
    public int? AverageDurationMinutes { get; set; }
    public int? PeakViewers { get; set; }
    public double? AverageViewers { get; set; }
    public int ChatAccounts { get; set; }
    public double? AverageChatAccounts { get; set; }

    public int NewCount { get; set; }
    public int ReturningCount { get; set; }
    public List<RecapViewerDto> NewViewers { get; set; } = [];
    public List<RecapViewerDto> ReturningViewers { get; set; } = [];
    public List<RecapViewerDto> StreakViewers { get; set; } = [];
    public List<RecapViewerDto> TopChatters { get; set; } = [];
    public List<RecapPlaythroughDto> Playthroughs { get; set; } = [];
}

public class RecapViewerDto
{
    public string TwitchUserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public int MinutesWatched { get; set; }
    public decimal ChatPoints { get; set; }
    /// <summary>Streams in a row the account missed before coming back (returning viewers).</summary>
    public int? MissedStreams { get; set; }
    /// <summary>Streams in a row, including this one, the account was in chat (regulars).</summary>
    public int? Streak { get; set; }
}

public class RecapPlaythroughDto
{
    public Guid PlaythroughId { get; set; }
    public string GameName { get; set; } = string.Empty;
    public string BoxArtUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    /// <summary>This stream's position within the playthrough (1-based).</summary>
    public int StreamNumber { get; set; }
    public int StreamsCount { get; set; }
}
