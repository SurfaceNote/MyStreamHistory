namespace MyStreamHistory.Shared.Base.Contracts.Auth.Requests;

/// <summary>
/// Request to get Twitch app access token from AuthService
/// </summary>
public class GetAppAccessTokenRequestContract
{
    /// <summary>
    /// Optional. Set to true to force refresh the token even if cached token is still valid
    /// </summary>
    public bool ForceRefresh { get; set; } = false;
}

