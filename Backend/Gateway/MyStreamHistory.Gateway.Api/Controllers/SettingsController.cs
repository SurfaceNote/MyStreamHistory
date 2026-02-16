using Microsoft.AspNetCore.Mvc;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using MyStreamHistory.Gateway.Application.Commands;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Gateway.Application.Queries;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Api.Wrappers;

namespace MyStreamHistory.Gateway.Api.Controllers;

[ApiController]
[Route("settings")]
[Authorize]
public class SettingsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("social-links")]
    [ProducesResponseType(typeof(ApiResultContainer<GetSocialLinksResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 401)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<GetSocialLinksResponseDto>>> GetSocialLinks()
    {
        var query = new GetSocialLinksQuery(UserId);
        var result = await mediator.Send(query);
        return this.Success(result);
    }

    [HttpPatch("social-links")]
    [ProducesResponseType(typeof(ApiResultContainer<UpdateSocialLinksResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 400)]
    [ProducesResponseType(typeof(ApiResultContainer), 401)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<UpdateSocialLinksResponseDto>>> UpdateSocialLinks(
        [FromBody] UpdateSocialLinksRequestDto request)
    {
        var command = new UpdateSocialLinksCommand(UserId, request.SocialLink);
        var result = await mediator.Send(command);
        
        if (!result.Success)
        {
            // Return 400 with error message in the response body
            return StatusCode(400, new ApiResultContainer<UpdateSocialLinksResponseDto>
            {
                Success = false,
                Data = result,
                Errors = null
            });
        }
        
        return this.Success(result);
    }
}

