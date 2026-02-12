namespace MyStreamHistory.Shared.Base.Contracts.Auth.Responses;

/// <summary>
/// Response containing Twitch app access token
/// </summary>
public class GetAppAccessTokenResponseContract
{
    /// <summary>
    /// Twitch app access token
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;
    
    /// <summary>
    /// Token expiration time in UTC
    /// </summary>
    public DateTime ExpiresAt { get; set; }
    
    /// <summary>
    /// Indicates if the request was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if request failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

