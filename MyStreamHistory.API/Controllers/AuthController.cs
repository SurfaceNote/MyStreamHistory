namespace MyStreamHistory.API.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;
    using MyStreamHistory.API.Enums;
    using MyStreamHistory.API.Models;
    using MyStreamHistory.API.Repositories;
    using MyStreamHistory.API.Services;
    using System.Security.Claims;

    [Route("auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ITwitchAuthService _twitchAuthService;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly JwtService _jwtService;
        private readonly IHttpContextAccessor _contextAccessor;
        private readonly IConfiguration _configuration;

        public AuthController(IUserRepository repository, ITwitchAuthService twitchAuthService, IRefreshTokenRepository refreshTokenRepository, JwtService jwtService, IHttpContextAccessor contextAccessor, IConfiguration configuration)
        {
            _userRepository = repository;
            _twitchAuthService = twitchAuthService;
            _refreshTokenRepository = refreshTokenRepository;
            _jwtService = jwtService;
            _contextAccessor = contextAccessor;
            _configuration = configuration;
        }

        [HttpGet("twitch/callback")]
        [AllowAnonymous]
        public async Task<ActionResult<TokenResponse>> TwitchCallback([FromQuery] TwitchCallbackRequest request)
        {
            if (string.IsNullOrEmpty(request.Code) || string.IsNullOrEmpty(request.State))
            {
                return BadRequest("Missing code or state.");
            }

            var twitchTokenResponse = await _twitchAuthService.ExchangeCodeForTokenAsync(request.Code);
            if (twitchTokenResponse == null || string.IsNullOrEmpty(twitchTokenResponse.AccessToken))
            {
                return BadRequest("Failed to retrieve access token.");
            }

            var twitchUser = await _twitchAuthService.GetUserInfoAsync(twitchTokenResponse.AccessToken);
            if (twitchUser == null)
            {
                return BadRequest("Failed to retrieve user information.");
            }

            ChannelStatusEnum broadcasterType = twitchUser.BroadcasterType switch
            {
                "affiliate" => ChannelStatusEnum.AFFILIATE,
                "partner" => ChannelStatusEnum.PARTNERED,
                _ => ChannelStatusEnum.SIMPLE
            };
            var existingUser = await _userRepository.GetStreamerByTwitchIdAsync(twitchUser.Id);

            var currentTime = DateTime.UtcNow;
            User user;

            if (existingUser == null)
            {
                user = new User
                {
                    TwitchId = twitchUser.Id,
                    Login = twitchUser.Login,
                    DisplayName = twitchUser.DisplayName,
                    Email = twitchUser.Email,
                    LogoUser = twitchUser.ProfileImageUrl,
                    BroadcasterType = broadcasterType,
                    TwitchCreatedAt = twitchUser.CreatedAt,
                    SiteCreatedAt = currentTime,
                    LastLoginAt = currentTime,
                    IsStreamer = false,
                    AccessToken = twitchTokenResponse.AccessToken,
                    RefreshToken = twitchTokenResponse.RefreshToken
                };
                user = await _userRepository.CreateUserAsync(user);
            }
            else
            {
                existingUser.Login = twitchUser.Login;
                existingUser.DisplayName = twitchUser.DisplayName;
                existingUser.Email = twitchUser.Email;
                existingUser.BroadcasterType = broadcasterType;
                existingUser.LastLoginAt = currentTime;
                existingUser.AccessToken = twitchTokenResponse.AccessToken;
                existingUser.RefreshToken = twitchTokenResponse.RefreshToken;
                existingUser.LogoUser = twitchUser.ProfileImageUrl;
                await _userRepository.UpdateUserAsync(existingUser);
                user = existingUser;
            }

            var accessToken = _jwtService.GenerateToken(user);

            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = _jwtService.GenerateRefreshToken(),
                ExpiresOnUtc = DateTime.UtcNow.AddDays(_configuration.GetValue<int>("JWT:ExpirationRefreshTokenInDays"))
            };

            await _refreshTokenRepository.CreateRefreshTokenAsync(refreshToken);

            HttpContext.Response.Cookies.Append("refresh_token", refreshToken.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = refreshToken.ExpiresOnUtc,
                Path = "/",
                IsEssential = true
            });

            return Ok(new TokenResponse { AccessToken = accessToken});

        }

        [HttpPost("refresh-token")]
        [AllowAnonymous]
        public async Task<ActionResult<TokenResponse>> RefreshToken()
        {
            var refreshTokenValue = Request.Cookies["refresh_token"];
            if (string.IsNullOrEmpty(refreshTokenValue))
            {
                return Unauthorized("Refresh token is missing");
            }

            var semaphore = _jwtService.GetRefreshLock(refreshTokenValue);
            await semaphore.WaitAsync();

            try
            {
                var refreshToken = await _refreshTokenRepository.GetRefreshTokenByToken(refreshTokenValue);
                if (refreshToken == null || refreshToken.ExpiresOnUtc < DateTime.UtcNow)
                {
                    return Unauthorized("Invalid or expired refresh token");
                }

                var newAccessToken = _jwtService.GenerateToken(refreshToken.User);

                refreshToken.Token = await GenerateUniqueRefreshToken();
                refreshToken.ExpiresOnUtc = DateTime.UtcNow.AddDays(_configuration.GetValue<int>("JWT:ExpirationRefreshTokenInDays"));
                await _refreshTokenRepository.UpdateRefreshToken(refreshToken);

                HttpContext.Response.Cookies.Append("refresh_token", refreshToken.Token, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = refreshToken.ExpiresOnUtc,
                    Path = "/",
                    IsEssential = true
                });

                return Ok(new TokenResponse { AccessToken = newAccessToken });
            }
            finally
            {
                _jwtService.ReleaseRefreshLock(refreshTokenValue);
            }
        }

        private async Task<string> GenerateUniqueRefreshToken()
        {
            const int maxAttempts = 5;
            for (int i = 0; i < maxAttempts; i++)
            {
                var token = _jwtService.GenerateRefreshToken();
                var existingToken = await _refreshTokenRepository.GetRefreshTokenByToken(token);
                if (existingToken == null)
                {
                    return token;
                }
            }
            throw new InvalidOperationException("Unable to generate a unique refresh token.");
        }
        
        private int GetCurrentUserId()
        {
            return int.TryParse(_contextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),
                out int parsed) ? parsed : 0;
        }
    }

    public class TwitchCallbackRequest
    {
        public string Code { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
    }
}
