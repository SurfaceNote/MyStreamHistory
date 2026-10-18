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

public class CommunityDailyState
{
    public string StreamerTwitchUserId { get; set; } = "";
    public int Revision { get; set; }
    public int MethodVersion { get; set; }
    public DateOnly Date { get; set; }
    public string ViewerTwitchUserId { get; set; } = "";
    public string Level { get; set; } = "inactive";
    public string Quality { get; set; } = "insufficient";
    public string? Transition { get; set; }
}
