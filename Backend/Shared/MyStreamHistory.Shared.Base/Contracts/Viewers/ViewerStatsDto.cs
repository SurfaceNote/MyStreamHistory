namespace MyStreamHistory.Shared.Base.Contracts.Viewers;

public class ViewerStatsDto
{
    public Guid Id { get; set; }
    public Guid ViewerId { get; set; }
    public string StreamerTwitchUserId { get; set; } = string.Empty;
    public int MinutesWatched { get; set; }
    public decimal EarnedMsgPoints { get; set; }
    public decimal Experience { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public int? Top100Rank { get; set; }
    public int StreamsWatched { get; set; }
    public DateTime? FirstWatchedAt { get; set; }
    public DateTime? LastWatchedAt { get; set; }
    public decimal WatchedPercentage { get; set; }
    public List<ViewerFavoriteCategoryDto> FavoriteCategories { get; set; } = new();
    public List<ViewerStreamHistoryDto> RecentStreams { get; set; } = new();
    
    // Navigation properties for responses
    public ViewerDto? Viewer { get; set; }
}

