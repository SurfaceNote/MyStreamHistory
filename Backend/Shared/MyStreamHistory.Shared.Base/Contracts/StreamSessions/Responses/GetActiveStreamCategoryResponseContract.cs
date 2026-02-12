namespace MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;

public class GetActiveStreamCategoryResponseContract
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public Guid? StreamSessionId { get; set; }
    public Guid? StreamCategoryId { get; set; }
    public string? CategoryName { get; set; }
}

