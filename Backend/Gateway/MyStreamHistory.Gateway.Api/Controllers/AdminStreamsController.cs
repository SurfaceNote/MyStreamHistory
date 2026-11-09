using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Gateway.Application.Queries;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Api.Wrappers;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions;

namespace MyStreamHistory.Gateway.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminStreamsRead")]
[Route("admin/streams")]
public class AdminStreamsController(ITransportBus bus, ILogger<AdminStreamsController> logger) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResultContainer<GetAdminStreamsResponse>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100) return BadRequest();
        Response.Headers.CacheControl = "no-store";
        var response = await bus.SendRequestAsync<GetAdminStreamsRequest, GetAdminStreamsResponse>(
            new() { Page = page, PageSize = pageSize }, ct);
        if (response.Success is not { } result) return StatusCode(503);
        if (result.Items.Count > 0)
        {
            try
            {
                var counts = await bus.SendRequestAsync<GetAdminStreamViewerCountsRequest, GetAdminStreamViewerCountsResponse>(
                    new() { Streams = result.Items.Select(s => new AdminStreamViewerScope
                        { StreamSessionId = s.Id, CategoryIds = s.CategoryIds }).ToList() }, ct);
                foreach (var item in result.Items)
                    if (counts.Success?.Counts.TryGetValue(item.Id, out var count) == true) item.UniqueViewers = count;
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Viewer counts unavailable for admin stream list");
            }
        }
        return this.Success(result);
    }

    /// <summary>The same recap the streamer sees on the stream page.</summary>
    [HttpGet("{streamId:guid}/recap")]
    public async Task<ActionResult<ApiResultContainer<StreamRecapDto>>> Recap(
        [FromRoute] Guid streamId, [FromServices] IMediator mediator, CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store";
        var recap = await mediator.Send(new GetStreamRecapQuery { StreamSessionId = streamId, AdminAccess = true }, ct);
        return this.Success(recap);
    }
}
