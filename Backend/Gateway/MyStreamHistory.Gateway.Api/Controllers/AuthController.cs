using Microsoft.AspNetCore.Mvc;
using AutoMapper;
using MediatR;
using MassTransit;
using MyStreamHistory.Shared.Api.Wrappers;
using MyStreamHistory.Shared.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Forms.Mapping;
using MyStreamHistory.Gateway.Application.Commands;
using MyStreamHistory.Shared.Api.Attributes;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;

namespace MyStreamHistory.Gateway.Api.Controllers

{
    [ApiController]
    [Route("auth")]
    public class AuthController(IMapper mapper, IMediator mediator, ITransportBus bus) : ApiControllerBase
    {
        private const string RefreshTokenCookieName = "__Host-refresh_token";

        [HttpGet("twitch/callback")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResultContainer<TwitchCallbackResponse>), 200)]
        [ProducesResponseType(typeof(ApiResultContainer), 500)]
        [ProducesResponseType(typeof(ApiResultContainer), 400)]
        public async Task<ActionResult<ApiResultContainer<TwitchCallbackResponse>>> TwitchCallback([FromQuery] TwitchCallbackRequest request)
        {
            var command = new TwitchCallbackCommand(request.Code, request.State, GetClientIpAddress());
            var twitchCallbackResultDto = await mediator.Send(command);
            var twitchCallbackResponse = mapper.Map<TwitchCallbackResponse>(twitchCallbackResultDto);
            SetRefreshTokenCookie(twitchCallbackResultDto.RefreshToken);

            return this.Success(twitchCallbackResponse);
        }

        [HttpPost("refresh-token")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResultContainer<RefreshTokenResponse>), 200)]
        [ProducesResponseType(typeof(ApiResultContainer), 500)]
        public async Task<ActionResult<ApiResultContainer<RefreshTokenResponse>>> RefreshToken(
            [FromBody] RefreshTokenRequest request)
        {
            var refreshToken = Request.Cookies[RefreshTokenCookieName];

            if (string.IsNullOrEmpty(refreshToken))
            {
                return this.Fail<RefreshTokenResponse>("invalid_token");
            }
            
            var command = new RefreshTokenCommand(refreshToken, GetClientIpAddress());
            var refreshResultDto = await mediator.Send(command);
            var refreshTokenResponse = mapper.Map<RefreshTokenResponse>(refreshResultDto);
            SetRefreshTokenCookie(refreshResultDto.RefreshToken);

            return this.Success(refreshTokenResponse);
        }

        [HttpPost("logout")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResultContainer), 200)]
        [ProducesResponseType(typeof(ApiResultContainer), 500)]
        public async Task<ActionResult<ApiResultContainer>> Logout()
        {
            var refreshToken = Request.Cookies[RefreshTokenCookieName];

            if (!string.IsNullOrEmpty(refreshToken))
            {
                await bus.SendRequestAsync<LogoutRequestContract, EmptyResponse, BaseFailedResponseContract>(
                    new LogoutRequestContract
                    {
                        Token = refreshToken
                    });
            }

            DeleteRefreshTokenCookie();
            return this.Success();
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            Response.Cookies.Append(RefreshTokenCookieName, refreshToken, BuildRefreshTokenCookieOptions());
        }

        private void DeleteRefreshTokenCookie()
        {
            Response.Cookies.Delete(RefreshTokenCookieName, BuildRefreshTokenCookieOptions());
        }

        private string? GetClientIpAddress()
        {
            return Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor)
                ? forwardedFor.ToString().Split(',')[0].Trim()
                : HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        private static CookieOptions BuildRefreshTokenCookieOptions()
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(365)
            };
        }
    }
}
