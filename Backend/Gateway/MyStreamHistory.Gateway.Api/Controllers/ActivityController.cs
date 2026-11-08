using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyStreamHistory.Gateway.Api.ProductActivity;
using MyStreamHistory.Shared.Base.Contracts.ProductActivity;

namespace MyStreamHistory.Gateway.Api.Controllers;

[ApiController]
[Route("activity")]
[Authorize]
public class ActivityController(ChannelActivityRecorder recorder) : ApiControllerBase
{
    /// <summary>Marks that the signed-in channel owner opened the website today.</summary>
    [HttpPost("visit")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> RecordVisit(CancellationToken cancellationToken)
    {
        await recorder.RecordAsync(TwitchUserId, ChannelActivityKinds.Visit, cancellationToken);
        return NoContent();
    }
}
