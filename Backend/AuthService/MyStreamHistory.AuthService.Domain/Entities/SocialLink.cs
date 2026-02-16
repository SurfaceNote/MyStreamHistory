namespace MyStreamHistory.AuthService.Domain.Entities;

public class SocialLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public SocialNetworkType SocialNetworkType { get; set; }
    public string Path { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public AuthUser? User { get; set; }
}

