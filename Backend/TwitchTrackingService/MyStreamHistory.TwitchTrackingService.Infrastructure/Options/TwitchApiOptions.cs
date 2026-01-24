using System.ComponentModel.DataAnnotations;

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Options;

public class TwitchApiOptions
{
    public const string SectionName = "TwitchApi";

    [Required]
    public string ClientId { get; init; } = null!;

    [Required]
    public string ClientSecret { get; init; } = null!;

    [Required]
    public string EventSubEndpoint { get; init; } = null!;

    [Required]
    public string CallbackUrl { get; init; } = null!;

    [Required]
    public string TokenEndpoint { get; init; } = null!;

    [Required]
    [MinLength(10)]
    public string WebhookSecret { get; init; } = null!;

}

