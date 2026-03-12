namespace MyStreamHistory.Shared.Base.Contracts.Playthroughs.Requests;

public class DeletePlaythroughRequestContract
{
    public int TwitchUserId { get; set; }
    public Guid PlaythroughId { get; set; }
}
