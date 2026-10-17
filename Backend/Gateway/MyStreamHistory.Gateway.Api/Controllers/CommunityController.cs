using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Api.Wrappers;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.Community;

namespace MyStreamHistory.Gateway.Api.Controllers;

[ApiController]
[Authorize]
[Route("analytics/community")]
public class CommunityController(ITransportBus bus, IConfiguration configuration) : ApiControllerBase
{
    [HttpGet]
    public Task<ActionResult<ApiResultContainer<CommunityResponse>>> Report([FromQuery] string? channelId, CancellationToken ct) => Send(new(), ct, channelId);

    [HttpGet("members")]
    public Task<ActionResult<ApiResultContainer<CommunityResponse>>> Members([FromQuery] string? level, [FromQuery] string? cursor, [FromQuery] string? channelId, CancellationToken ct) =>
        Send(new() { Operation = "members", Level = level, Cursor = cursor }, ct, channelId);

    [HttpPut("settings")]
    public Task<ActionResult<ApiResultContainer<CommunityResponse>>> Settings([FromBody] CommunitySettingsRequest request, CancellationToken ct) =>
        Send(new() { Operation = "settings", TimeZoneId = request.TimeZoneId }, ct);

    [HttpGet("exclusions")]
    public Task<ActionResult<ApiResultContainer<CommunityResponse>>> Exclusions([FromQuery] string? channelId, CancellationToken ct) => Send(new() { Operation = "exclusions" }, ct, channelId);

    [HttpPut("exclusions/{twitchUserId}")]
    public Task<ActionResult<ApiResultContainer<CommunityResponse>>> Exclude(string twitchUserId, CancellationToken ct) =>
        Send(new() { Operation = "exclude", ViewerTwitchUserId = twitchUserId }, ct);

    [HttpDelete("exclusions/{twitchUserId}")]
    public Task<ActionResult<ApiResultContainer<CommunityResponse>>> Restore(string twitchUserId, CancellationToken ct) =>
        Send(new() { Operation = "restore", ViewerTwitchUserId = twitchUserId }, ct);

    private async Task<ActionResult<ApiResultContainer<CommunityResponse>>> Send(CommunityRequest request, CancellationToken ct, string? channelId = null)
    {
        if (!int.TryParse(User.FindFirst("TwitchId")?.Value, out var owner) || owner <= 0) return Forbid();
        var target = owner;
        if (channelId != null && (!int.TryParse(channelId, out target) || target <= 0)) return BadRequest(new ApiResultContainer { Success = false, Errors = ["invalidChannel"] });
        var admin = User.IsInRole("admin");
        if (target != owner && !admin) return Forbid();
        // Cross-channel access is read only, including callers attempting to attach a target to a write.
        if (request.Operation is "settings" or "exclude" or "restore"
            && Request.Query.TryGetValue("channelId", out var writeTarget)
            && writeTarget.ToString() != owner.ToString()) return Forbid();
        request.StreamerTwitchUserId = target.ToString();
        var pilots = configuration.GetSection("Features:CommunityAnalytics:PilotTwitchIds").Get<string[]>();
        if (!admin && pilots is { Length: > 0 } && !pilots.Contains(request.StreamerTwitchUserId)) return NotFound();
        var response = await bus.SendRequestAsync<CommunityRequest, CommunityResponse>(request, ct);
        if (response.Success == null) return StatusCode(503, new ApiResultContainer { Success = false, Errors = ["communityUnavailable"] });
        if (response.Success.Error != null) return BadRequest(new ApiResultContainer { Success = false, Errors = [response.Success.Error] });
        response.Success.ChannelTwitchUserId = request.StreamerTwitchUserId;
        response.Success.CanViewOtherChannels = admin;
        response.Success.ReadOnly = target != owner;
        Response.Headers.CacheControl = "no-store";
        return this.Success(response.Success);
    }
}

public class CommunitySettingsRequest
{
    public string TimeZoneId { get; set; } = "UTC";
}
