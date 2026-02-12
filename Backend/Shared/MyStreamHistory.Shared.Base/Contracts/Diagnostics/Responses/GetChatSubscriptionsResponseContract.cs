namespace MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;

/// <summary>
/// Response with chat message EventSub subscriptions
/// </summary>
public class GetChatSubscriptionsResponseContract
{
    public int Count { get; set; }
    public List<ChatSubscriptionDto> Subscriptions { get; set; } = new();
}

public class ChatSubscriptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BroadcasterUserId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

