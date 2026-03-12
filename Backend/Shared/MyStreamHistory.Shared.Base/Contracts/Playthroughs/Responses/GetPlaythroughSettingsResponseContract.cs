namespace MyStreamHistory.Shared.Base.Contracts.Playthroughs.Responses;

public class GetPlaythroughSettingsResponseContract
{
    public PlaythroughSettingsDto Settings { get; set; } = new();
}
