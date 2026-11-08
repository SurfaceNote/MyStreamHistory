using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyStreamHistory.Gateway.Application.Queries;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Api.Wrappers;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.ProductActivity;
using MyStreamHistory.Shared.Base.Contracts.Users;
using MyStreamHistory.Shared.Base.Contracts.Viewers;

namespace MyStreamHistory.Gateway.Api.Controllers;

/// <summary>Read-only product metrics for the admin panel.</summary>
[ApiController]
[Authorize(Policy = "AdminProductRead")]
[Route("admin/product")]
public class AdminProductController(ITransportBus bus, IMediator mediator, ILogger<AdminProductController> logger) : ApiControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<ApiResultContainer<GetProductOverviewResponse>>> Overview(
        [FromQuery] int weeks = 12, CancellationToken ct = default)
    {
        if (weeks is < 1 or > 52) return BadRequest();
        Response.Headers.CacheControl = "no-store";
        var response = await bus.SendRequestAsync<GetProductOverviewRequest, GetProductOverviewResponse>(new() { Weeks = weeks }, ct);
        if (response.Success is not { } overview) return StatusCode(503);

        // Registered channels without any recorded data still belong in the table; names come from AuthService.
        var users = await AdminUsers.LoadAsync(mediator, logger, ct);
        var known = overview.Channels.ToDictionary(c => c.TwitchUserId);
        foreach (var user in users)
        {
            if (known.TryGetValue(user.TwitchId, out var channel))
            {
                channel.Login = user.Login ?? channel.Login;
                channel.DisplayName = user.DisplayName ?? channel.DisplayName;
                channel.AvatarUrl = string.IsNullOrEmpty(user.Avatar) ? channel.AvatarUrl : user.Avatar;
            }
            else
            {
                overview.Channels.Add(new ProductChannelDto
                {
                    TwitchUserId = user.TwitchId, Login = user.Login, DisplayName = user.DisplayName,
                    AvatarUrl = user.Avatar, Tracking = "missing"
                });
            }
        }
        return this.Success(overview);
    }
}

/// <summary>Admin-managed list of chat bots that are excluded from audience rankings.</summary>
[ApiController]
[Authorize(Policy = "AdminBotsManage")]
[Route("admin/bots")]
public class AdminBotsController(ITransportBus bus, IMediator mediator, ILogger<AdminBotsController> logger) : ApiControllerBase
{
    public sealed record BotRequest(string Login);

    [HttpGet]
    public async Task<ActionResult<ApiResultContainer<GetRankingExclusionsResponse>>> List(CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store";
        var response = await bus.SendRequestAsync<GetRankingExclusionsRequest, GetRankingExclusionsResponse>(new(), ct);
        if (response.Success is not { } result) return StatusCode(503);

        var names = (await AdminUsers.LoadAsync(mediator, logger, ct)).ToDictionary(u => u.TwitchId.ToString(), u => u.DisplayName);
        foreach (var channel in result.Candidates.SelectMany(c => c.Channels))
            channel.StreamerDisplayName = names.GetValueOrDefault(channel.StreamerTwitchUserId);
        return this.Success(result);
    }

    [HttpPost]
    public Task<ActionResult<ApiResultContainer<ChangeRankingExclusionResponse>>> Add([FromBody] BotRequest request, CancellationToken ct) =>
        Change(request.Login, exclude: true, ct);

    [HttpDelete("{login}")]
    public Task<ActionResult<ApiResultContainer<ChangeRankingExclusionResponse>>> Remove([FromRoute] string login, CancellationToken ct) =>
        Change(login, exclude: false, ct);

    private async Task<ActionResult<ApiResultContainer<ChangeRankingExclusionResponse>>> Change(string login, bool exclude, CancellationToken ct)
    {
        var normalized = RankingExclusions.NormalizeLogin(login);
        if (normalized == null) return BadRequest();
        var actor = User.Identity?.Name ?? "admin";
        var response = await bus.SendRequestAsync<ChangeRankingExclusionRequest, ChangeRankingExclusionResponse>(
            new() { Login = normalized, Actor = actor, Exclude = exclude }, ct);
        if (response.Success is not { } result) return StatusCode(503);
        if (!result.Success) return BadRequest();
        return this.Success(result);
    }
}

internal static class AdminUsers
{
    public static async Task<List<UserDto>> LoadAsync(IMediator mediator, ILogger logger, CancellationToken ct)
    {
        try
        {
            return await mediator.Send(new GetAllUsersQuery(), ct);
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(exception, "User list unavailable for an admin view");
            return [];
        }
    }
}
