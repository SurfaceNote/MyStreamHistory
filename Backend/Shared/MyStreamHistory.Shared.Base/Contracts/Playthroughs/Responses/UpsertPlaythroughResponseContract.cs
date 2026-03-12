namespace MyStreamHistory.Shared.Base.Contracts.Playthroughs.Responses;

public class UpsertPlaythroughResponseContract
{
    public PlaythroughDto Playthrough { get; set; } = new();
}
