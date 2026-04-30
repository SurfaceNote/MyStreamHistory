namespace MyStreamHistory.AuthService.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid TokenId { get; set; }
    public Guid TokenFamilyId { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? CreatedByIp { get; set; }

    public AuthUser? User { get; set; }
}
