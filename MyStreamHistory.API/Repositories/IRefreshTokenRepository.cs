namespace MyStreamHistory.API.Repositories
{
    using MyStreamHistory.API.Models;
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken> CreateRefreshTokenAsync(RefreshToken refreshToken);
        Task<RefreshToken?> GetRefreshTokenByToken(string token);
        Task UpdateRefreshToken(RefreshToken refreshToken);
        Task DeleteRefreshTokenAsync(RefreshToken refreshToken);
    }
}
