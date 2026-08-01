namespace MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;

public class GetUniqueViewerCountsResponseContract
{
    public List<UniqueViewerCountDto> Counts { get; set; } = new();
}
