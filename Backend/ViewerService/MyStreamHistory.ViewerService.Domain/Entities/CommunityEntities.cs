namespace MyStreamHistory.ViewerService.Domain.Entities;

public class CommunitySettings
{
    public string StreamerTwitchUserId { get; set; } = "";
    public string TimeZoneId { get; set; } = "UTC";
    public bool TimeZoneConfigured { get; set; }
    public int Revision { get; set; }
    public bool Pending { get; set; } = true;
    public DateTime? TrackingStartedAt { get; set; }
    public DateTime? CalculatedAt { get; set; }
    /// <summary>Last chat roster poll. A report is rebuilt once polling stops (the broadcast has ended).</summary>
    public DateTime? LastActivityAt { get; set; }
    public string? ReportJson { get; set; }
    public string? MembersJson { get; set; }
}

public class CommunityPoll
{
    public string StreamerTwitchUserId { get; set; } = "";
    public DateTime Minute { get; set; }
    public Guid StreamSessionId { get; set; }
    public DateTime ObservedAt { get; set; }
    public bool Complete { get; set; }
}

// A unique minute, rather than elapsed watch time or an incrementing counter.
public class CommunityPresence
{
    public string StreamerTwitchUserId { get; set; } = "";
    public string ViewerTwitchUserId { get; set; } = "";
    public Guid StreamSessionId { get; set; }
    public DateTime Minute { get; set; }
}

public class CommunityMessageReceipt
{
    public string StreamerTwitchUserId { get; set; } = "";
    public string MessageId { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
}

public class CommunityMessageAggregate
{
    public string StreamerTwitchUserId { get; set; } = "";
    public Guid StreamSessionId { get; set; }
    public string ViewerTwitchUserId { get; set; } = "";
    public int Count { get; set; }
    public bool Provisional { get; set; }
}

public class CommunityExclusion
{
    public string StreamerTwitchUserId { get; set; } = "";
    public string ViewerTwitchUserId { get; set; } = "";
}

/// <summary>Per-session roll-up of minute polls, built once after the session ends.</summary>
public class CommunitySessionSummary
{
    public string StreamerTwitchUserId { get; set; } = "";
    public Guid StreamSessionId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public int ExpectedMinutes { get; set; }
    public int CompleteMinutes { get; set; }
    public int LongestGap { get; set; }
    /// <summary>Estimated from category watch totals recorded before minute polling started.</summary>
    public bool Legacy { get; set; }
    public DateTime AggregatedAt { get; set; }
}

/// <summary>One row per viewer per session: distinct minutes in chat. Replaces reading raw minutes for every report.</summary>
public class CommunitySessionVisit
{
    public string StreamerTwitchUserId { get; set; } = "";
    public Guid StreamSessionId { get; set; }
    public string ViewerTwitchUserId { get; set; } = "";
    public int Minutes { get; set; }
    /// <summary>Historical sessions only: the viewer earned chat points, so they wrote in chat.</summary>
    public bool LegacyChat { get; set; }
}
