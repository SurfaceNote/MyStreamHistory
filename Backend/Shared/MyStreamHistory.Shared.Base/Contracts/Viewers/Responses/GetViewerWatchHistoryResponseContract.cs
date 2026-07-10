namespace MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;

public class GetViewerWatchHistoryResponseContract
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int StreamsWatched { get; set; }
    public DateTime? FirstWatchedAt { get; set; }
    public DateTime? LastWatchedAt { get; set; }
    public decimal WatchedPercentage { get; set; }
    public List<ViewerFavoriteCategoryDto> FavoriteCategories { get; set; } = new();
    public List<ViewerStreamHistoryDto> RecentStreams { get; set; } = new();
}
