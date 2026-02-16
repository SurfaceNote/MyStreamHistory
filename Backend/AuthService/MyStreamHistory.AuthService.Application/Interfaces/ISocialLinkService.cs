using MyStreamHistory.AuthService.Domain.Entities;

namespace MyStreamHistory.AuthService.Application.Interfaces;

public interface ISocialLinkService
{
    Task<List<SocialLink>> GetSocialLinksByUserIdAsync(Guid userId);
    Task<(bool Success, string? ErrorMessage)> AddSocialLinkAsync(Guid userId, SocialNetworkType type, string path);
    Task<(bool Success, string? ErrorMessage)> UpdateSocialLinkAsync(Guid userId, SocialNetworkType type, string path);
    Task<(bool Success, string? ErrorMessage)> DeleteSocialLinkAsync(Guid userId, SocialNetworkType type);
}

