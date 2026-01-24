using System.ComponentModel.DataAnnotations;

namespace MyStreamHistory.Gateway.Application.Options;

public class TwitchEventSubOptions
{
    public const string SectionName = "TwitchEventSub";

    [Required]
    public string Secret { get; set; } = string.Empty;

    [Range(0, 120)]
    public int MaxAgeMinutes { get; set; } = 10;
}