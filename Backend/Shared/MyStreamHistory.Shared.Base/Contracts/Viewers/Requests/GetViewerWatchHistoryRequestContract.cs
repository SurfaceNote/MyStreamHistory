namespace MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;

public class GetViewerWatchHistoryRequestContract
{
    public int StreamerTwitchUserId { get; set; }
    public int RecentStreamsLimit { get; set; } = 10;
    public List<ViewerCategoryWatchDto> CategoryWatches { get; set; } = new();
}
