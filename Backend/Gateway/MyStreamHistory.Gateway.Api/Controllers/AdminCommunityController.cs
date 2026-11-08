using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Api.Wrappers;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.Community;

namespace MyStreamHistory.Gateway.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminCommunityRead")]
[Route("admin/community")]
public class AdminCommunityController(ITransportBus bus, ILogger<AdminCommunityController> logger) : ApiControllerBase
{
    [HttpGet]
    public Task<ActionResult<ApiResultContainer<CommunityResponse>>> Report([FromQuery] string? channelId, CancellationToken ct) => Send(new(), channelId, ct);
    [HttpGet("members")]
    public Task<ActionResult<ApiResultContainer<CommunityResponse>>> Members([FromQuery] string? channelId, [FromQuery] string? level,
        [FromQuery] string? cursor, CancellationToken ct) => Send(new() { Operation = "members", Level = level, Cursor = cursor }, channelId, ct);
    [HttpGet("exclusions")]
    public Task<ActionResult<ApiResultContainer<CommunityResponse>>> Exclusions([FromQuery] string? channelId, CancellationToken ct) =>
        Send(new() { Operation = "exclusions" }, channelId, ct);

    private async Task<ActionResult<ApiResultContainer<CommunityResponse>>> Send(CommunityRequest request, string? channelId, CancellationToken ct)
    {
        if (!int.TryParse(channelId, out var target) || target <= 0)
            return BadRequest(new ApiResultContainer { Success = false, Errors = ["invalidChannel"] });
        request.StreamerTwitchUserId = target.ToString();
        var response = await bus.SendRequestAsync<CommunityRequest, CommunityResponse>(request, ct);
        if (response.Success == null) return StatusCode(503, new ApiResultContainer { Success = false, Errors = ["communityUnavailable"] });
        if (response.Success.Error != null) return BadRequest(new ApiResultContainer { Success = false, Errors = [response.Success.Error] });
        response.Success.ChannelTwitchUserId = request.StreamerTwitchUserId;
        response.Success.CanViewOtherChannels = true; response.Success.ReadOnly = true;
        Response.Headers.CacheControl = "no-store";
        logger.LogInformation("AUDIT Admin {Actor} read Community {Operation} for channel {Channel}; RequestId {RequestId}",
            User.Identity?.Name, request.Operation, channelId, HttpContext.TraceIdentifier);
        return this.Success(response.Success);
    }
}
