namespace MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;

/// <summary>
/// Response for subscribe to all users operation
/// </summary>
public class SubscribeToAllUsersResponseContract
{
    public int UserCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

