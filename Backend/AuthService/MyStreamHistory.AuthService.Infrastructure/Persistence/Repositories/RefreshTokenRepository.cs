using Microsoft.EntityFrameworkCore;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Domain.Entities;
using MyStreamHistory.Shared.Infrastructure.Persistence.Repository;

namespace MyStreamHistory.AuthService.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository(AuthDbContext dbContext) : RepositoryBase<RefreshToken, AuthDbContext>(dbContext), IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByTokenIdAndHashAsync(Guid tokenId, string tokenHash, CancellationToken cancellationToken = default)
    {
        return await Query()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenId == tokenId && rt.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<List<RefreshToken>> GetByFamilyIdAsync(Guid tokenFamilyId, CancellationToken cancellationToken = default)
    {
        return await Query()
            .Where(rt => rt.TokenFamilyId == tokenFamilyId)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<RefreshToken>> GetExpiredAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        return await Query()
            .Where(rt => rt.ExpiresAt < now)
            .ToListAsync(cancellationToken);
    }
}
