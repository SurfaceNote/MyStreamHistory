using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyStreamHistory.Gateway.Application.Queries;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Api.Wrappers;
using MyStreamHistory.Shared.Base.Contracts.Users;

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
}