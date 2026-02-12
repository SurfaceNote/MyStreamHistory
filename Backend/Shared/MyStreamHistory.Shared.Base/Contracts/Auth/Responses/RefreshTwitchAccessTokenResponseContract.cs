namespace MyStreamHistory.Shared.Base.Contracts.Auth.Responses;

public class RefreshTwitchAccessTokenResponseContract
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? AccessToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

