namespace MyStreamHistory.Shared.Base.Contracts.Auth.Requests;

public class GetTwitchAccessTokenRequestContract
{
    public string TwitchUserId { get; set; } = string.Empty;
    public string RequestingService { get; set; } = string.Empty;
}

