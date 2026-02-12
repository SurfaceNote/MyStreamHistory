namespace MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;

/// <summary>
/// Response after cleaning up chat message EventSub subscriptions
/// </summary>
public class CleanupChatSubscriptionsResponseContract
{
    public int DeletedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

