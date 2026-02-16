namespace MyStreamHistory.Shared.Base.Contracts.Auth.Requests;

public class UpdateSocialLinksRequestContract
{
    public Guid UserId { get; set; }
    public List<SocialLinkDto> SocialLinks { get; set; } = new();
}

