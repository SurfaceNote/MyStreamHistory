using MyStreamHistory.AuthService.Domain.Entities;
using MyStreamHistory.Shared.Application.Repository;

namespace MyStreamHistory.AuthService.Application.Interfaces;

public interface IRefreshTokenRepository : IRepositoryBase<RefreshToken>
{
    
}