using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Gateway.Application.Queries;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Api.Wrappers;

namespace MyStreamHistory.Gateway.Api.Controllers;

[ApiController]
[Route("streams")]
public class StreamsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public StreamsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{streamId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResultContainer<StreamDetailsDto>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 404)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<StreamDetailsDto>>> GetStreamDetails(
        [FromRoute] Guid streamId)
    {
        var query = new GetStreamDetailsByIdQuery 
        { 
            StreamSessionId = streamId 
        };
        
        var streamDetails = await _mediator.Send(query);
        return this.Success(streamDetails);
    }

    /// <summary>
    /// Stream recap: comparison with recent streams, first-time and returning chat accounts.
    /// Only the streamer can read it; the admin panel uses <c>admin/streams/{id}/recap</c>.
    /// </summary>
    [HttpGet("{streamId:guid}/recap")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResultContainer<StreamRecapDto>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 401)]
    [ProducesResponseType(typeof(ApiResultContainer), 403)]
    [ProducesResponseType(typeof(ApiResultContainer), 404)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<StreamRecapDto>>> GetStreamRecap([FromRoute] Guid streamId)
    {
        Response.Headers.CacheControl = "private, no-store";
        var recap = await _mediator.Send(new GetStreamRecapQuery { StreamSessionId = streamId, RequesterTwitchUserId = TwitchUserId });
        return this.Success(recap);
    }
}

