using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Domain.Entities;
using MyStreamHistory.Shared.Infrastructure.Persistence.Repository;

namespace MyStreamHistory.AuthService.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository(AuthDbContext dbContext) : RepositoryBase<RefreshToken, AuthDbContext>(dbContext), IRefreshTokenRepository
{
    
}