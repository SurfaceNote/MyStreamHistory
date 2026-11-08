namespace MyStreamHistory.Shared.Base.Contracts.ProductActivity;

/// <summary>
/// Product activity of a registered channel. Only the channel owner's Twitch ID is recorded,
/// at most once per kind and UTC day; viewer identities are never part of these events.
/// </summary>
public static class ChannelActivityKinds
{
    /// <summary>The channel owner signed in for the first time.</summary>
    public const string Registered = "registered";

    /// <summary>The signed-in channel owner opened the website.</summary>
    public const string Visit = "visit";

    /// <summary>The OBS top viewers overlay of the channel requested data.</summary>
    public const string Overlay = "overlay";

    /// <summary>The Twitch panel extension of the channel requested data.</summary>
    public const string Panel = "panel";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Registered, Visit, Overlay, Panel
    };
}

public class ChannelActivityObservedContract
{
    public int TwitchUserId { get; init; }
    public string Kind { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
}

public class GetCollectionStatusRequestContract
{
    public int TwitchUserId { get; init; }
}

public class GetCollectionStatusResponseContract
{
    public bool DataCollectionEnabled { get; init; }
    public List<CollectionSubscriptionDto> Subscriptions { get; init; } = [];
    public int StreamsCount { get; init; }
    public bool IsLive { get; init; }
    public DateTime? LastStreamStartedAt { get; init; }
    public DateTime? LastStreamEndedAt { get; init; }
    public DateTime? LastOverlaySeenAt { get; init; }
    public DateTime? LastPanelSeenAt { get; init; }
}

public class CollectionSubscriptionDto
{
    public string EventType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
