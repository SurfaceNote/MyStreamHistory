namespace MyStreamHistory.Shared.Base.Contracts.Viewers;

public class ViewerDto
{
    public Guid Id { get; set; }
    public string TwitchUserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>The account is on the admin-managed bot list and is not ranked as audience.</summary>
    public bool IsRankingExcluded { get; set; }
}

