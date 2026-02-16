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

namespace MyStreamHistory.Gateway.Api.Controllers

{
    [ApiController]
    [Route("auth")]
    public class AuthController(IMapper mapper, IMediator mediator) : ApiControllerBase
    {
        [HttpGet("twitch/callback")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResultContainer<TwitchCallbackResponse>), 200)]
        [ProducesResponseType(typeof(ApiResultContainer), 500)]
        [ProducesResponseType(typeof(ApiResultContainer), 400)]
        public async Task<ActionResult<ApiResultContainer<TwitchCallbackResponse>>> TwitchCallback([FromQuery] TwitchCallbackRequest request)
        {
            var command = mapper.Map<TwitchCallbackCommand>(request);
            var twitchCallbackResultDto = await mediator.Send(command);
            var twitchCallbackResponse = mapper.Map<TwitchCallbackResponse>(twitchCallbackResultDto);

            return this.Success(twitchCallbackResponse);
        }

        [HttpPost("refresh-token")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResultContainer<RefreshTokenResponse>), 200)]
        [ProducesResponseType(typeof(ApiResultContainer), 500)]
        public async Task<ActionResult<ApiResultContainer<RefreshTokenResponse>>> RefreshToken(
            [FromBody] RefreshTokenRequest request)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return this.Fail<RefreshTokenResponse>("invalid_token");
            }
            
            var command = new RefreshTokenCommand(userId, request.Token);
            var refreshResultDto = await mediator.Send(command);
            var refreshTokenResponse = mapper.Map<RefreshTokenResponse>(refreshResultDto);

            return this.Success(refreshTokenResponse);
        }
    }
}
