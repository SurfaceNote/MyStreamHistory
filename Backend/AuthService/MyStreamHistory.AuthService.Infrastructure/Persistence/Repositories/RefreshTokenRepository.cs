using Microsoft.EntityFrameworkCore;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Domain.Entities;
using MyStreamHistory.Shared.Infrastructure.Persistence.Repository;

namespace MyStreamHistory.AuthService.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository(AuthDbContext dbContext) : RepositoryBase<RefreshToken, AuthDbContext>(dbContext), IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByUserIdAndTokenAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        return await Query()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.UserId == userId && rt.Token == token, cancellationToken);
    }
}