using System.Net.Http.Headers;

namespace MyStreamHistory.TwitchBot.Services;

public class TwitchAuthHandler(
    TwitchAuthService twitchAuthService, IConfiguration config) 
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await twitchAuthService.GetAppAccessTokenAsync(cancellationToken);
        var clientId = config["Twitch:ClientId"];

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (!request.Headers.Contains("Client-Id"))
        {
            request.Headers.Add("Client-Id", clientId);
        }
        
        return await base.SendAsync(request, cancellationToken);
    }
}