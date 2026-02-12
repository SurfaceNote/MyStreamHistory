namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface ITwitchChatApiClient
{
    Task<List<TwitchChatterDto>> GetChattersAsync(string broadcasterId, string accessToken, CancellationToken cancellationToken = default);
}

public class TwitchChatterDto
{
    public string UserId { get; set; } = string.Empty;
    public string UserLogin { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}

