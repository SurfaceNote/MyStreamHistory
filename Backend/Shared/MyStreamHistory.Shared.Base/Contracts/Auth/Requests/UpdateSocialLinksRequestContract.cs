namespace MyStreamHistory.Shared.Base.Contracts.Auth.Requests;

public class UpdateSocialLinksRequestContract
{
    public Guid UserId { get; set; }
    public SocialLinkDto SocialLink { get; set; } = null!;
}

