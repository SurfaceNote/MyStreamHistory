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
}

