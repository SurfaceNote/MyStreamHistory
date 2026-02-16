using Microsoft.EntityFrameworkCore;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Domain.Entities;
using MyStreamHistory.Shared.Infrastructure.Persistence.Repository;

namespace MyStreamHistory.AuthService.Infrastructure.Persistence.Repositories;

public class SocialLinkRepository(AuthDbContext dbContext) : RepositoryBase<SocialLink, AuthDbContext>(dbContext), ISocialLinkRepository
{
    public async Task<List<SocialLink>> GetByUserIdAsync(Guid userId)
    {
        return await Query()
            .Where(sl => sl.UserId == userId)
            .OrderBy(sl => sl.SocialNetworkType)
            .ToListAsync();
    }

    public async Task<SocialLink?> GetByUserIdAndTypeAsync(Guid userId, SocialNetworkType type)
    {
        return await Query()
            .FirstOrDefaultAsync(sl => sl.UserId == userId && sl.SocialNetworkType == type);
    }

    public async Task<bool> ExistsAsync(Guid userId, SocialNetworkType type)
    {
        return await Query()
            .AnyAsync(sl => sl.UserId == userId && sl.SocialNetworkType == type);
    }
}

