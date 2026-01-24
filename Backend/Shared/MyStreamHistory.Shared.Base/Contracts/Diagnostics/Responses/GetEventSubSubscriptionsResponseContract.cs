namespace MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;

/// <summary>
/// Response containing all EventSub subscriptions
/// </summary>
public class GetEventSubSubscriptionsResponseContract
{
    public EventSubSubscriptionsDto Subscriptions { get; set; } = new();
}

public class EventSubSubscriptionsDto
{
    public List<EventSubSubscriptionDto> Data { get; set; } = new();
    public int Total { get; set; }
    public int TotalCost { get; set; }
    public int MaxTotalCost { get; set; }
}

public class EventSubSubscriptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Cost { get; set; }
    public EventSubConditionDto Condition { get; set; } = new();
    public EventSubTransportDto Transport { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class EventSubConditionDto
{
    public string BroadcasterUserId { get; set; } = string.Empty;
}

public class EventSubTransportDto
{
    public string Method { get; set; } = string.Empty;
    public string Callback { get; set; } = string.Empty;
}

