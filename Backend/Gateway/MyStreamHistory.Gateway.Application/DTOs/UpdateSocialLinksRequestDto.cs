namespace MyStreamHistory.Gateway.Application.DTOs;

public class UpdateSocialLinksRequestDto
{
    public List<SocialLinkDto> SocialLinks { get; set; } = new();
}

