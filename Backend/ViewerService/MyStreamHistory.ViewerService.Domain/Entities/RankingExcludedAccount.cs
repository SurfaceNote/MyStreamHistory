namespace MyStreamHistory.ViewerService.Domain.Entities;

/// <summary>A chat account (usually a bot) that is not ranked as part of any channel's audience.</summary>
public class RankingExcludedAccount
{
    /// <summary>Twitch login in lower case.</summary>
    public string Login { get; set; } = null!;
    public string AddedBy { get; set; } = null!;
    public DateTime AddedAt { get; set; }
}
