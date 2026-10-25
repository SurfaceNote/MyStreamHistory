using System.ComponentModel.DataAnnotations;

namespace MyStreamHistory.AuthService.Infrastructure.Options;

public sealed class TwitchProfileSyncOptions
{
    public const string SectionName = "TwitchProfileSync";

    [Range(1, 1440)]
    public int IntervalMinutes { get; init; } = 60;
}
