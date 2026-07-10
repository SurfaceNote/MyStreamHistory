namespace MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;

public class GetStreamerViewerStatsResponseContract
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public ViewerStatsDto? Stats { get; set; }
    public List<ViewerCategoryWatchDto> CategoryWatches { get; set; } = new();
}
