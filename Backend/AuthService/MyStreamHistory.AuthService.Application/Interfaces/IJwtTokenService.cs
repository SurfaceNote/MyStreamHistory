using MyStreamHistory.AuthService.Domain.Entities;

namespace MyStreamHistory.AuthService.Application.Interfaces;

public interface IJwtTokenService
{
    public string GenerateAccessToken(AuthUser user);

    public string GenerateRefreshToken();
}