namespace MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;

/// <summary>
/// Response for delete all subscriptions operation
/// </summary>
public class DeleteAllSubscriptionsResponseContract
{
    public int DeletedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

