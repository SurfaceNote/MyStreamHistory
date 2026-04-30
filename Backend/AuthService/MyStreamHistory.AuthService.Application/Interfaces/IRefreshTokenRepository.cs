using MyStreamHistory.AuthService.Domain.Entities;
using MyStreamHistory.Shared.Application.Repository;

namespace MyStreamHistory.AuthService.Application.Interfaces;

public interface IRefreshTokenRepository : IRepositoryBase<RefreshToken>
{
    Task<RefreshToken?> GetByTokenIdAndHashAsync(Guid tokenId, string tokenHash, CancellationToken cancellationToken = default);
    Task<List<RefreshToken>> GetByFamilyIdAsync(Guid tokenFamilyId, CancellationToken cancellationToken = default);
    Task<List<RefreshToken>> GetExpiredAsync(DateTime now, CancellationToken cancellationToken = default);
}
