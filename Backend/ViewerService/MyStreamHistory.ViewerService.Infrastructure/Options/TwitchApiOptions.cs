namespace MyStreamHistory.ViewerService.Infrastructure.Options;

public class TwitchApiOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.twitch.tv/helix";
}

