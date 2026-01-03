using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using MyStreamHistory.AuthService.Application.DTOs.Twitch;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Infrastructure.Options;

namespace MyStreamHistory.AuthService.Infrastructure.Services;

public class TwitchAuthService(HttpClient httpClient, IOptions<TwitchOptions> twitchOptions) : ITwitchAuthService
{
    public async Task<TokenResponseDto?> ExchangeCodeForTokenAsync(string code)
    {
        var requestContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", twitchOptions.Value.ClientId),
            new KeyValuePair<string, string>("client_secret", twitchOptions.Value.ClientSecret),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("redirect_uri", twitchOptions.Value.RedirectUri)
        });

        using var response = await httpClient.PostAsync(twitchOptions.Value.TokenEndpoint, requestContent);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var tokenResponseDto = await response.Content.ReadFromJsonAsync<TokenResponseDto>();

        return tokenResponseDto ?? null;
    }

    public async Task<TwitchUserDto?> GetUserInfoAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, twitchOptions.Value.UsersEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Client-Id", twitchOptions.Value.ClientId);

        using var response = await httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        

        var twitchUserResponseDto = await response.Content.ReadFromJsonAsync<TwitchUserResponseDto>();
        
        var twitchUserDto = twitchUserResponseDto?.TwitchUsers.FirstOrDefault();
        
        return twitchUserDto ?? null;
    }
}