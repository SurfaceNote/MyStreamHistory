namespace MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;

public class GetStreamViewersRequestContract
{
    public Guid StreamSessionId { get; set; }
    public List<Guid> StreamCategoryIds { get; set; } = new();
}

