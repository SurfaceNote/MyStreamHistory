namespace MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;

public class GetTopViewersResponseContract
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ViewerStatsDto> TopViewers { get; set; } = new();
}

