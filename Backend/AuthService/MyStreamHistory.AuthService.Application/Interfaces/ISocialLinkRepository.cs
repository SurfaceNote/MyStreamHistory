using MyStreamHistory.AuthService.Domain.Entities;
using MyStreamHistory.Shared.Application.Repository;

namespace MyStreamHistory.AuthService.Application.Interfaces;

public interface ISocialLinkRepository : IRepositoryBase<SocialLink>
{
    Task<List<SocialLink>> GetByUserIdAsync(Guid userId);
    Task<SocialLink?> GetByUserIdAndTypeAsync(Guid userId, SocialNetworkType type);
    Task<bool> ExistsAsync(Guid userId, SocialNetworkType type);
}

