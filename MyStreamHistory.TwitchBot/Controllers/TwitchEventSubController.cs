using Microsoft.AspNetCore.Mvc;
using MyStreamHistory.TwitchBot.DTOs;
using MyStreamHistory.TwitchBot.Services;

namespace MyStreamHistory.TwitchBot.Controllers;

[ApiController]
[Route("twitch/eventsub/callback")]
public class TwitchEventSubController : ControllerBase
{
    public async Task<IActionResult> HandleAsync([FromServices] ITwitchEventDispatcher dispatcher)
    {
        var payload = await Request.ReadFromJsonAsync<TwitchEventSubNotificationDto>();
        if (payload is null)
        {
            return BadRequest();
        }

        if (!string.IsNullOrWhiteSpace(payload.Challenge))
        {
            return Content(payload.Challenge, "text/plain", System.Text.Encoding.UTF8); 
        }
        await dispatcher.DispatchAsync(payload, HttpContext.RequestAborted);
        return NoContent();
    }    
}