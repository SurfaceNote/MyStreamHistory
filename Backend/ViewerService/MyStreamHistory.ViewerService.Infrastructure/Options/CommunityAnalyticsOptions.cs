using System.ComponentModel.DataAnnotations;

namespace MyStreamHistory.ViewerService.Infrastructure.Options;

public sealed class CommunityAnalyticsOptions
{
    public const string SectionName = "CommunityAnalytics";

    [Range(1, 3650)]
    public int MessageReceiptRetentionDays { get; set; } = 7;
}
