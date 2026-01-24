using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyStreamHistory.Gateway.Application.Queries;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Api.Wrappers;
using MyStreamHistory.Shared.Base.Contracts.Users;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions;

namespace MyStreamHistory.Gateway.Api.Controllers;

[ApiController]
[Route("user")]
public class UsersController(IMapper mapper, IMediator mediator) : ApiControllerBase
{
    [HttpGet("get-new-users")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResultContainer<List<UserDto>>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    [ProducesResponseType(typeof(ApiResultContainer), 400)]
    public async Task<ActionResult<ApiResultContainer<List<UserDto>>>> TwitchCallback()
    {
        var usersDto = await mediator.Send(new GetNewUsersQuery());

        return this.Success(usersDto);
    }

    [HttpGet("{twitchId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResultContainer<UserDto>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    [ProducesResponseType(typeof(ApiResultContainer), 400)]
    public async Task<ActionResult<ApiResultContainer<UserDto>>> GetUserByTwitchId([FromRoute] int twitchId)
    {
        var userDto = await mediator.Send(new GetUserByTwitchIdQuery(twitchId));

        return this.Success(userDto);
    }

    [HttpGet("{twitchId}/recent-streams")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResultContainer<List<StreamSessionDto>>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    [ProducesResponseType(typeof(ApiResultContainer), 400)]
    public async Task<ActionResult<ApiResultContainer<List<StreamSessionDto>>>> GetRecentStreams(
        [FromRoute] int twitchId,
        [FromQuery] int count = 10)
    {
        var query = new GetRecentStreamsByTwitchIdQuery 
        { 
            TwitchUserId = twitchId, 
            Count = count 
        };
        var streams = await mediator.Send(query);

        return this.Success(streams);
    }
}