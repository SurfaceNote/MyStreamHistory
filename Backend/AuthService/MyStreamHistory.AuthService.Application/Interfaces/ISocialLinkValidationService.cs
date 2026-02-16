using MyStreamHistory.AuthService.Domain.Entities;

namespace MyStreamHistory.AuthService.Application.Interfaces;

public interface ISocialLinkValidationService
{
    bool ValidateLink(SocialNetworkType type, string path, out string? errorMessage);
}

