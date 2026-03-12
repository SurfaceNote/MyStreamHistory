namespace MyStreamHistory.Shared.Base.Contracts.Playthroughs.Requests;

public class UpsertPlaythroughRequestContract
{
    public Guid? Id { get; set; }
    public int TwitchUserId { get; set; }
    public Guid TwitchCategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool AutoAddNewStreams { get; set; }
    public List<Guid> StreamCategoryIds { get; set; } = new();
}
