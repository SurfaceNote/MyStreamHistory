namespace MyStreamHistory.API.Services
{
    using MyStreamHistory.API.Models;
    
    public interface ITwitchAuthService
    {
        Task<TokenResponse> ExchangeCodeForTokenAsync(string code);
        Task<TwitchUser> GetUserInfoAsync(string accessToken);
    }
}
