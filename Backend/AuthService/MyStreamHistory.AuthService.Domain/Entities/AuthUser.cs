namespace MyStreamHistory.AuthService.Domain.Entities;

public class AuthUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TwitchId { get; set; }
    public string? Email { get; set; }
    public string Login { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? TwitchAccessToken { get; set; }
    public string? TwitchRefreshToken { get; set; }
    public bool IsTwitchTokenFresh { get; set; }

    public DateTime TwitchCreatedAt { get; set; }
    public DateTime SiteCreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime LastLoginAt { get; set; }
}