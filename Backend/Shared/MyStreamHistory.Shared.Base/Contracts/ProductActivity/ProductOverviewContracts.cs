namespace MyStreamHistory.Shared.Base.Contracts.ProductActivity;

public class GetProductOverviewRequest
{
    public int Weeks { get; init; } = 12;
}

public class GetProductOverviewResponse
{
    public DateTime GeneratedAt { get; init; }

    /// <summary>First day with recorded product activity; weeks before it cannot count engaged channels.</summary>
    public DateOnly? ActivityTrackedSince { get; init; }

    /// <summary>False when the Twitch subscription list could not be read; tracking is then "unknown".</summary>
    public bool SubscriptionsAvailable { get; init; }

    public List<ProductWeekDto> Weeks { get; init; } = [];
    public List<ActivationCohortDto> Activation { get; init; } = [];
    public List<ProductChannelDto> Channels { get; init; } = [];
}

public class ProductWeekDto
{
    public DateOnly WeekStart { get; init; }
    public int ChannelsStreamed { get; init; }
    public int EngagedChannels { get; init; }
}

public class ActivationCohortDto
{
    public DateOnly WeekStart { get; init; }
    public int Registered { get; init; }
    public int StreamedWithin14Days { get; init; }
    public int Activated { get; init; }
}

public class ProductChannelDto
{
    public int TwitchUserId { get; init; }
    public string? Login { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime? RegisteredAt { get; init; }
    public int StreamsCount { get; init; }
    public int StreamsLast28Days { get; init; }
    public DateTime? LastStreamStartedAt { get; init; }
    public bool IsLive { get; init; }

    /// <summary>active, pending, missing, paused, or unknown when Twitch could not be read.</summary>
    public string Tracking { get; init; } = "missing";

    public DateTime? LastVisitAt { get; init; }
    public DateTime? LastOverlayAt { get; init; }
    public DateTime? LastPanelAt { get; init; }
}
