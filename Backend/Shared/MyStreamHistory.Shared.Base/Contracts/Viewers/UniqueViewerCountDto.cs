namespace MyStreamHistory.Shared.Base.Contracts.Viewers;

public class UniqueViewerCountDto
{
    public Guid PlaythroughId { get; set; }
    public int UniqueViewerCount { get; set; }
}
