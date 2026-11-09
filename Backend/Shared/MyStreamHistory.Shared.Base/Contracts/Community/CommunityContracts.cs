namespace MyStreamHistory.Shared.Base.Contracts.Community;

public class CommunityRequest
{
    public string StreamerTwitchUserId { get; set; } = "";
    public string Operation { get; set; } = "report";
    public string? TimeZoneId { get; set; }
    public string? ViewerTwitchUserId { get; set; }
    /// <summary>Segment filter for the members list (see <see cref="CommunitySegments"/>), or null for everyone.</summary>
    public string? Level { get; set; }
    /// <summary>Chat activity filter: "chatty" or "silent".</summary>
    public string? Activity { get; set; }
    public string? Search { get; set; }
    public string? Cursor { get; set; }
}

public class CommunityResponse
{
    public string? ChannelTwitchUserId { get; set; }
    public bool CanViewOtherChannels { get; set; }
    public bool ReadOnly { get; set; }
    public string? Error { get; set; }
    public CommunityReport? Report { get; set; }
    public List<CommunityMember> Members { get; set; } = [];
    public int? MembersTotal { get; set; }
    public string? NextCursor { get; set; }
    public List<CommunityExcludedAccount> Exclusions { get; set; } = [];
    /// <summary>Admin overview: one row per channel that has community data.</summary>
    public List<CommunityChannelSummary> Channels { get; set; } = [];
}

public class CommunityChannelSummary
{
    public string TwitchUserId { get; set; } = "";
    public string Quality { get; set; } = "insufficient";
    public bool Recalculating { get; set; }
    public int TotalBroadcasts { get; set; }
    public Dictionary<string, int> Segments { get; set; } = [];
    public DateOnly? AsOf { get; set; }
    public DateTime? CalculatedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

public static class CommunitySegments
{
    public const string Returned = "returned", New = "new", Lost = "lost", Fading = "fading", Core = "core",
        Regular = "regular", Occasional = "occasional", OneTime = "oneTime";

    /// <summary>Priority order: a viewer gets the first segment whose rule matches.</summary>
    public static readonly string[] All = [Returned, New, Lost, Fading, Core, Regular, Occasional, OneTime];
    public const string Chatty = "chatty", Silent = "silent";
}

public class CommunityReport
{
    public const int CurrentMethodVersion = 2;
    public int MethodVersion { get; set; } = CurrentMethodVersion;
    public DateTime? CalculatedAt { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public bool TimeZoneConfigured { get; set; }
    /// <summary>insufficient: fewer broadcasts than needed for core/regular; provisional: partial window or historical estimates; verified.</summary>
    public string Quality { get; set; } = "insufficient";
    public bool Recalculating { get; set; }
    public bool CollectionEnabled { get; set; }
    public int WindowSize { get; set; }
    public int MinBroadcasts { get; set; }
    public int TotalBroadcasts { get; set; }
    public int SkippedBroadcasts { get; set; }
    public decimal? Coverage { get; set; }
    /// <summary>Local dates of the broadcasts in the current window, oldest first. Member attendance strings align with it.</summary>
    public List<DateOnly> WindowDates { get; set; } = [];
    public DateOnly? From { get; set; }
    public DateOnly? AsOf { get; set; }
    public Dictionary<string, int> Segments { get; set; } = [];
    public Dictionary<string, int>? PreviousSegments { get; set; }
    public DateOnly? PreviousDate { get; set; }
    public Dictionary<string, int> Activity { get; set; } = [];
    public CommunityLastBroadcast? LastBroadcast { get; set; }
    public CommunityRetention? Retention { get; set; }
    public List<CommunityTrendPoint> Trend { get; set; } = [];
}

public class CommunityLastBroadcast
{
    public DateOnly Date { get; set; }
    public int Visitors { get; set; }
    public int NewCount { get; set; }
    public int ReturnedCount { get; set; }
    public int MissingCoreCount { get; set; }
    public List<CommunityAccount> New { get; set; } = [];
    public List<CommunityAccount> Returned { get; set; } = [];
    public List<CommunityAccount> MissingCore { get; set; } = [];
}

public class CommunityAccount
{
    public string TwitchUserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

/// <summary>Viewers whose first visit falls into the window (except the last few broadcasts, which are too recent to judge).</summary>
public class CommunityRetention
{
    public int Newcomers { get; set; }
    public int CameBack { get; set; }
    public int Stayed { get; set; }
}

public class CommunityTrendPoint
{
    public DateOnly Date { get; set; }
    /// <summary>Segment sizes after this broadcast. Absent while core/regular cannot be determined yet.</summary>
    public Dictionary<string, int> Segments { get; set; } = [];
    public int Visitors { get; set; }
    public int NewViewers { get; set; }
    public int Returned { get; set; }
    public int BecameFading { get; set; }
    public int BecameLost { get; set; }
    public bool Provisional { get; set; }
}

public class CommunityMember
{
    public string TwitchUserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Login { get; set; } = "";
    public string? ProfileImageUrl { get; set; }
    public string Segment { get; set; } = CommunitySegments.OneTime;
    public string? Activity { get; set; }
    public int VisitsInWindow { get; set; }
    public int WindowSize { get; set; }
    public int TotalVisits { get; set; }
    public int MissedStreak { get; set; }
    public int? MessagesInWindow { get; set; }
    public int MinutesInWindow { get; set; }
    public DateOnly? FirstSeen { get; set; }
    public DateOnly? LastSeen { get; set; }
    /// <summary>One character per broadcast in <see cref="CommunityReport.WindowDates"/>: 1 visited, 0 absent.</summary>
    public string Attendance { get; set; } = "";
}

public class CommunityExcludedAccount
{
    public string TwitchUserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public class GetCommunitySessionsRequest
{
    public int TwitchUserId { get; set; }
}

public class GetCommunitySessionsResponse
{
    public List<CommunitySession> Sessions { get; set; } = [];
}

public class CommunitySession
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsLive { get; set; }
    public List<Guid> CategoryIds { get; set; } = [];
}
