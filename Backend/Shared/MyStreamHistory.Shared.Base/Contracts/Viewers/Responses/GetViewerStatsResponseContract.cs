namespace MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;

public class GetViewerStatsResponseContract
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public ViewerDto? Viewer { get; set; }
    public List<ViewerCategoryWatchDto> CategoryStats { get; set; } = new();
    public int TotalMinutesWatched { get; set; }
    public decimal TotalChatPoints { get; set; }
}

