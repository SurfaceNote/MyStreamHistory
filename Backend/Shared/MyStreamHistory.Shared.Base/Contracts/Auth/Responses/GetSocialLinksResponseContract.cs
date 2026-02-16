namespace MyStreamHistory.Shared.Base.Contracts.Auth.Responses;

public class GetSocialLinksResponseContract
{
    public List<SocialLinkDto> SocialLinks { get; set; } = new();
}

