namespace MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;

public class GetUniqueViewerCountsRequestContract
{
    public List<PlaythroughViewerCountRequestContract> Playthroughs { get; set; } = new();
}

public class PlaythroughViewerCountRequestContract
{
    public Guid PlaythroughId { get; set; }
    public List<Guid> StreamCategoryIds { get; set; } = new();
}
