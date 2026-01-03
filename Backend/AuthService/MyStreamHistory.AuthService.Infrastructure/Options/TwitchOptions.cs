using System.ComponentModel.DataAnnotations;

namespace MyStreamHistory.AuthService.Infrastructure.Options;

public class TwitchOptions
{
    public const string SectionName = "Twitch";

    [Required]
    public string ClientId { get; init; } = null!;
    
    [Required]
    public string ClientSecret { get; init; } = null!;

    /// <summary>
    /// https://localhost:5000/auth/twitch/token
    /// https://api.mystreamhistory.com/auth/twitch/token
    /// </summary>
    [Required]
    public string RedirectUri { get; init; } = null!;

    [Required]
    public string TokenEndpoint { get; init; } = null!;

    [Required]
    public string UsersEndpoint { get; init; } = null!;
}