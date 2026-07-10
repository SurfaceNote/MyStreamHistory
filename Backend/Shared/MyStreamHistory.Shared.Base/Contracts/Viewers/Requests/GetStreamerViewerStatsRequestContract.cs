namespace MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;

public class GetStreamerViewerStatsRequestContract
{
    public string StreamerTwitchUserId { get; set; } = string.Empty;
    public string ViewerTwitchUserId { get; set; } = string.Empty;
}
