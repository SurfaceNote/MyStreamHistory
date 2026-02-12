namespace MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;

public class GetStreamViewersResponseContract
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ViewerCategoryWatchDto> Viewers { get; set; } = new();
}

