namespace MyStreamHistory.Shared.Base.Contracts.Auth;

public class SocialLinkDto
{
    public string SocialNetworkType { get; set; } = null!;
    public string Path { get; set; } = null!;
    public string FullUrl { get; set; } = null!;
}

