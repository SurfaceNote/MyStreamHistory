namespace MyStreamHistory.Shared.Base.Contracts.Community;

public class CommunityRequest
{
    public string StreamerTwitchUserId { get; set; } = "";
    public string Operation { get; set; } = "report";
    public string? TimeZoneId { get; set; }
    public string? ViewerTwitchUserId { get; set; }
    public string? Level { get; set; }
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
    public string? NextCursor { get; set; }
    public List<CommunityExcludedAccount> Exclusions { get; set; } = [];
}

public class CommunityReport
{
    public const int CurrentMethodVersion = 1;
    public int MethodVersion { get; set; } = CurrentMethodVersion;
    public DateTime? CalculatedAt { get; set; }
    public DateOnly? AsOf { get; set; }
    public DateOnly? From { get; set; }
    public int WindowDays { get; set; } = 28;
    public string TimeZoneId { get; set; } = "UTC";
    public bool TimeZoneConfigured { get; set; }
    public string Quality { get; set; } = "insufficient";
    public bool Recalculating { get; set; }
    public bool CollectionEnabled { get; set; }
    public int? EligibleDays { get; set; }
    public int? UnknownDays { get; set; }
    public decimal? Coverage { get; set; }
    public int? Core { get; set; }
    public int? PreviousCore { get; set; }
    public int? RegularIncludingCore { get; set; }
    public int? FirstObserved { get; set; }
    public int? Declining { get; set; }
    public Dictionary<string, int>? Distribution { get; set; }
    public List<CommunityTrendPoint> Trend { get; set; } = [];
    public Dictionary<string, int> Transitions { get; set; } = [];
}

public class CommunityTrendPoint
{
    public DateOnly Date { get; set; }
    public int? Core { get; set; }
    public string Quality { get; set; } = "insufficient";
}

public class CommunityMember
{
    public string TwitchUserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Login { get; set; } = "";
    public string? ProfileImageUrl { get; set; }
    public string Level { get; set; } = "inactive";
    public string Quality { get; set; } = "provisional";
    public int VisitedDays { get; set; }
    public int EligibleDays { get; set; }
    public decimal? Regularity { get; set; }
    public int? MessageCount { get; set; }
    public decimal? MessagePoints { get; set; }
    public int? DaysWithMessages { get; set; }
    public bool HasHistoricalChat { get; set; }
    public bool MessagesProvisional { get; set; }
    public DateOnly? FirstObserved { get; set; }
    public int ConsecutiveMisses { get; set; }
    public bool Declining { get; set; }
    public bool Dormant { get; set; }
    public bool Returned { get; set; }
    public string? Transition { get; set; }
    public List<CommunityDayHistory> History { get; set; } = [];
}

public class CommunityDayHistory
{
    public DateOnly Date { get; set; }
    public string Status { get; set; } = "unknown";
    public string Quality { get; set; } = "provisional";
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
