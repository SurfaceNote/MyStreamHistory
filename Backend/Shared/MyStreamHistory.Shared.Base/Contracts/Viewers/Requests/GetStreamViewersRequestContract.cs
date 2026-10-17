namespace MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;

public class GetStreamViewersRequestContract
{
    public Guid StreamSessionId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IncludeSessionPresence { get; set; }
    public List<Guid> StreamCategoryIds { get; set; } = new();
}

