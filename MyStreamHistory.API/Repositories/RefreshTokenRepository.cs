namespace MyStreamHistory.API.Repositories
{
    using Microsoft.EntityFrameworkCore;
    using MyStreamHistory.API.Data;
    using MyStreamHistory.API.Models;
    using System.Runtime.InteropServices;

    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly AppDbContext _appDbContext;

        public RefreshTokenRepository(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task<RefreshToken> CreateRefreshTokenAsync(RefreshToken refreshToken)
        {
            _appDbContext.RefreshTokens.Add(refreshToken);
            await _appDbContext.SaveChangesAsync();
            return refreshToken;
        }

        public async Task<RefreshToken?> GetRefreshTokenByToken(string token)
        {
            return await _appDbContext.RefreshTokens.Include(r => r.User).FirstOrDefaultAsync(r => r.Token == token);
        }

        public async Task UpdateRefreshToken(RefreshToken refreshToken)
        {
            _appDbContext.Entry(refreshToken).State = EntityState.Modified;
            await _appDbContext.SaveChangesAsync();
        }

        public async Task DeleteRefreshTokenAsync(RefreshToken refreshToken)
        {
            _appDbContext.RefreshTokens.Remove(refreshToken);
            await _appDbContext.SaveChangesAsync();
        }
    }
}
