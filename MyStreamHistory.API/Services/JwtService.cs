namespace MyStreamHistory.API.Services
{
    using Microsoft.IdentityModel.Tokens;
    using MyStreamHistory.API.Models;
    using System.Collections.Concurrent;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using System.Security.Cryptography;
    using System.Text;

    public class JwtService(IConfiguration _configuration, 
                            IHttpContextAccessor _httpContextAccessor)
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshLocks = new();

        public string GenerateToken(User user)
        {
            var securityKeyString = _configuration["JWT:Secret"] ?? "";
            securityKeyString = securityKeyString?.Replace("%JWT_SECRET%", Environment.GetEnvironmentVariable("JWT_SECRET")) ?? "";
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityKeyString));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, user.Login),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? "No email")
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("JWT:ExpirationAccessTokenInMinutes")),
                SigningCredentials = credentials,
                Issuer = _configuration["JWT:Issuer"],
                Audience = _configuration["JWT:Audience"]
            };

            var handler = new JwtSecurityTokenHandler();
            var token = handler.CreateToken(tokenDescriptor);
            return handler.WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }

        public SemaphoreSlim GetRefreshLock(string refreshToken)
        {
            return _refreshLocks.GetOrAdd(refreshToken, _ => new SemaphoreSlim(1, 1));
        }

        public void ReleaseRefreshLock(string refreshToken)
        {
            if (_refreshLocks.TryRemove(refreshToken, out var semaphore))
            {
                semaphore.Release();
                semaphore.Dispose();
            }
        }

        public void AddTokenToCookie(string name, string value, DateTimeOffset expires)
        {
            _httpContextAccessor?.HttpContext?.Response.Cookies.Append(name, value, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = expires,
                Path = "/"
            });
        }

        public void RemoveTokenFromCookie(string name)
        {
            _httpContextAccessor?.HttpContext?.Response.Cookies.Delete(name, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var securityKeyString = _configuration["JWT:Secret"] ?? "";
                securityKeyString = securityKeyString?.Replace("%JWT_SECRET%", Environment.GetEnvironmentVariable("JWT_SECRET")) ?? "";

                var tokenHandler = new JwtSecurityTokenHandler();
                var _tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = _configuration["JWT:Issuer"],
                    ValidAudience = _configuration["JWT:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityKeyString))
                };

                var principal = tokenHandler.ValidateToken(token, _tokenValidationParameters, out var validatedToken);
                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}
