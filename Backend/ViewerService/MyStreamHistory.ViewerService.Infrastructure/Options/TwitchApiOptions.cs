namespace MyStreamHistory.ViewerService.Infrastructure.Options;

public class TwitchApiOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.twitch.tv/helix";
    public string EventSubCallbackUrl { get; set; } = string.Empty;
    public string EventSubSecret { get; set; } = string.Empty;
}

