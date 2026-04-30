using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Domain.Entities;
using JwtRegisteredClaimNames = Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames;

namespace MyStreamHistory.AuthService.Infrastructure.Services;

public class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public string GenerateAccessToken(AuthUser user)
    {
        var adminTwitchIds = configuration
            .GetSection("Jwt:AdminTwitchIds")
            .Get<int[]>() ?? [];

        var claimList = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),

            new("UserName", user.DisplayName),
            new("TwitchId", user.TwitchId.ToString()),
        };

        if (adminTwitchIds.Contains(user.TwitchId))
        {
            claimList.Add(new Claim("role", "admin"));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claimList,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken(Guid tokenId)
    {
        var tokenIdPart = Base64UrlEncode(tokenId.ToByteArray());
        var secretPart = Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        return $"{tokenIdPart}.{secretPart}";
    }

    public bool TryReadRefreshTokenId(string token, out Guid tokenId)
    {
        tokenId = Guid.Empty;

        var separatorIndex = token.IndexOf('.');
        if (separatorIndex <= 0)
        {
            return false;
        }

        try
        {
            tokenId = new Guid(Base64UrlDecode(token[..separatorIndex]));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string HashRefreshToken(string token)
    {
        var pepper = configuration["RefreshToken:Pepper"] ?? configuration["Jwt:Key"]!;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(pepper));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(token)));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value
            .Replace('-', '+')
            .Replace('_', '/');

        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Convert.FromBase64String(base64);
    }
}
