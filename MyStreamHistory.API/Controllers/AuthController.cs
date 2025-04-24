namespace MyStreamHistory.API.Controllers
{
    using Microsoft.AspNetCore.Mvc;
    using MyStreamHistory.API.DTOs;
    using MyStreamHistory.API.Enums;
    using MyStreamHistory.API.Models;
    using MyStreamHistory.API.Repositories;
    using MyStreamHistory.API.Services;

    [Route("auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ITwitchAuthService _twitchAuthService;


        public AuthController(IUserRepository repository, ITwitchAuthService twitchAuthService)
        {
            _userRepository = repository;
            _twitchAuthService = twitchAuthService;
        }

        [HttpGet("twitch/callback")]
        public async Task<ActionResult<string>> TwitchCallback(string code, string scope, string state)
        {
            var referer = Request.Headers["Referer"].ToString();

            if (string.IsNullOrEmpty(referer) || referer != "https://id.twitch.tv/")
            {
                return BadRequest("Invalid referer.");
            }

            if (string.IsNullOrEmpty(code))
            {
                return BadRequest("Authorization code is missing.");
            }

            if (string.IsNullOrEmpty(state))
            {
                return BadRequest("State parameter is missing");
            }

            if (string.IsNullOrEmpty(scope))
            {
                return BadRequest("Scope parameter is missing.");
            }

            var tokenResponse = await _twitchAuthService.ExchangeCodeForTokenAsync(code);
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AcessToken))
            {
                return BadRequest("Failed to retrieve access token.");
            }

            var twitchUser = await _twitchAuthService.GetUserInfoAsync(tokenResponse.AcessToken);
            if (twitchUser == null)
            {
                return BadRequest("Failed to retrieve user information.");
            }

            ChannelStatusEnum broadcasterType = ChannelStatusEnum.SIMPLE;
            switch (twitchUser.BroadcasterType)
            {
                case "affiliate":
                    {
                        broadcasterType = ChannelStatusEnum.AFFILIATE;
                        break;
                    }
                case "partner":
                    {
                        broadcasterType = ChannelStatusEnum.PARTNERED;
                        break;
                    }
            }
            var t = ChannelStatusEnum.AFFILIATE;
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
                    AccessToken = tokenResponse.AcessToken,
                    RefreshToken = tokenResponse.RefreshToken
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
                existingUser.AccessToken = tokenResponse.AcessToken;
                existingUser.RefreshToken = tokenResponse.RefreshToken;
                existingUser.LogoUser = twitchUser.ProfileImageUrl;
                await _userRepository.UpdateUserAsync(existingUser);
                user = existingUser;
            }



            return Ok($"User {user.DisplayName} was created!");

        }

    }
}
