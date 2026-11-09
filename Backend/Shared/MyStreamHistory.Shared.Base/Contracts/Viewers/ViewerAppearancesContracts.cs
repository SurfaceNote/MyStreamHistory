namespace MyStreamHistory.Shared.Base.Contracts.Viewers;

/// <summary>
/// Which earlier streams of a channel the given chat accounts appeared on. Used by the stream recap
/// to tell first-time chat accounts from regulars and from people returning after a break.
/// </summary>
public class GetViewerAppearancesRequest
{
    public string StreamerTwitchUserId { get; set; } = string.Empty;
    public List<string> ViewerTwitchUserIds { get; set; } = [];
    /// <summary>Earlier streams of the channel with their category segments.</summary>
    public List<ViewerAppearanceSession> Sessions { get; set; } = [];
    /// <summary>Streams whose audience size (without the broadcaster and bots) is returned for comparison.</summary>
    public List<Guid> CountSessionIds { get; set; } = [];
}

public class ViewerAppearanceSession
{
    public Guid Id { get; set; }
    public List<Guid> CategoryIds { get; set; } = [];
}

public class GetViewerAppearancesResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ViewerAppearanceDto> Viewers { get; set; } = [];
    public List<SessionAudienceCountDto> SessionAudienceCounts { get; set; } = [];
}

public class ViewerAppearanceDto
{
    public string ViewerTwitchUserId { get; set; } = string.Empty;
    public List<Guid> SessionIds { get; set; } = [];
}

public class SessionAudienceCountDto
{
    public Guid SessionId { get; set; }
    public int Count { get; set; }
}
