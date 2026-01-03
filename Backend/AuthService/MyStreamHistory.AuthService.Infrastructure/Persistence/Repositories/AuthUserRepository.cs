using Microsoft.EntityFrameworkCore;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Domain.Entities;
using MyStreamHistory.Shared.Infrastructure.Persistence.Repository;

namespace MyStreamHistory.AuthService.Infrastructure.Persistence.Repositories;

public class AuthUserRepository(AuthDbContext dbContext) : RepositoryBase<AuthUser, AuthDbContext>(dbContext), IAuthUserRepository
{
    public Task<AuthUser?> GetUserByTwitchIdAsync(int twitchUserId)
    {
        return Query().FirstOrDefaultAsync(x => x.TwitchId == twitchUserId);
    }
}