using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Gateway.Application.Queries;
using MyStreamHistory.Gateway.Api.ProductActivity;
using MyStreamHistory.Shared.Base.Contracts.ProductActivity;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Api.Wrappers;
using MyStreamHistory.Shared.Base.Contracts.Users;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions;
using MyStreamHistory.Shared.Base.Contracts.Viewers;

namespace MyStreamHistory.Gateway.Api.Controllers;

[ApiController]
[Route("user")]
public class UsersController(IMapper mapper, IMediator mediator, ChannelActivityRecorder activity) : ApiControllerBase
{
    [HttpGet("all")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResultContainer<List<UserDto>>), 200)]
    public async Task<ActionResult<ApiResultContainer<List<UserDto>>>> GetAllUsers(CancellationToken cancellationToken)
    {
        return this.Success(await mediator.Send(new GetAllUsersQuery(), cancellationToken));
    }

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
        [FromQuery] int count = 10,
        [FromQuery] DateTime? before = null)
    {
        var query = new GetRecentStreamsByTwitchIdQuery 
        { 
            TwitchUserId = twitchId, 
            Count = Math.Clamp(count, 1, 100),
            Before = before?.ToUniversalTime()
        };
        var streams = await mediator.Send(query);

        return this.Success(streams);
    }

    [HttpGet("{twitchId}/top-viewers")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResultContainer<List<ViewerStatsDto>>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    [ProducesResponseType(typeof(ApiResultContainer), 400)]
    public async Task<ActionResult<ApiResultContainer<List<ViewerStatsDto>>>> GetTopViewers(
        [FromRoute] int twitchId,
        [FromQuery] int limit = 100,
        [FromQuery] string? source = null)
    {
        if (string.Equals(source, "overlay", StringComparison.OrdinalIgnoreCase))
        {
            await activity.RecordAsync(twitchId, ChannelActivityKinds.Overlay, HttpContext.RequestAborted);
        }

        var query = new GetTopViewersQuery 
        { 
            StreamerTwitchUserId = twitchId.ToString(), 
            Limit = limit 
        };
        var topViewers = await mediator.Send(query);

        return this.Success(topViewers);
    }

    [HttpGet("{twitchId}/viewers/{viewerTwitchId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResultContainer<ViewerStatsDto>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 404)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<ViewerStatsDto>>> GetViewerStats(
        [FromRoute] int twitchId,
        [FromRoute] string viewerTwitchId)
    {
        var stats = await mediator.Send(new GetStreamerViewerStatsQuery(
            twitchId.ToString(),
            viewerTwitchId));

        return this.Success(stats);
    }

    [HttpGet("{twitchId}/social-links")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResultContainer<GetSocialLinksResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 404)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<GetSocialLinksResponseDto>>> GetSocialLinksByTwitchId(
        [FromRoute] int twitchId)
    {
        var query = new GetSocialLinksByTwitchIdQuery(twitchId);
        var result = await mediator.Send(query);
        return this.Success(result);
    }

    [HttpGet("{twitchId}/statistics")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResultContainer<StreamerStatisticsDto>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<StreamerStatisticsDto>>> GetStreamerStatistics(
        [FromRoute] int twitchId,
        [FromQuery] string period = "30d")
    {
        if (ChannelActivityRecorder.IsTwitchExtensionRequest(Request))
        {
            await activity.RecordAsync(twitchId, ChannelActivityKinds.Panel, HttpContext.RequestAborted);
        }

        var query = new GetStreamerStatisticsQuery { TwitchUserId = twitchId, Period = period };
        var result = await mediator.Send(query);
        return this.Success(result);
    }
}
