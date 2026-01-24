using MediatR;
using Microsoft.AspNetCore.Mvc;
using MyStreamHistory.Gateway.Application.Commands.TwitchEventSub;

namespace MyStreamHistory.Gateway.Api.Controllers;

[ApiController]
[Route("api/eventsub")]
public class TwitchEventSubController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<TwitchEventSubController> _logger;

    public TwitchEventSubController(IMediator mediator, ILogger<TwitchEventSubController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("callback")]
    public async Task<IActionResult> HandleEventSubCallback(CancellationToken cancellationToken)
    {
        try
        {
            // Read raw request body
            using var reader = new StreamReader(Request.Body);
            var requestBody = await reader.ReadToEndAsync(cancellationToken);

            // Get headers
            var messageId = Request.Headers["Twitch-Eventsub-Message-Id"].ToString();
            var messageTimestamp = Request.Headers["Twitch-Eventsub-Message-Timestamp"].ToString();
            var messageSignature = Request.Headers["Twitch-Eventsub-Message-Signature"].ToString();
            var messageType = Request.Headers["Twitch-Eventsub-Message-Type"].ToString();

            _logger.LogInformation("Received EventSub callback - Type: {MessageType}, ID: {MessageId}", 
                messageType, messageId);

            if (string.IsNullOrEmpty(messageId) || string.IsNullOrEmpty(messageTimestamp) || 
                string.IsNullOrEmpty(messageSignature) || string.IsNullOrEmpty(messageType))
            {
                _logger.LogWarning("Missing required Twitch EventSub headers. MessageId: {MessageId}, MessageTimestamp: {MessageTimestamp}, MessageSignature: {MessageSignature}, MessageType: {MessageType}", 
                    messageId, messageTimestamp, messageSignature, messageType);
                return BadRequest("Missing required headers");
            }

            var command = new ProcessEventSubCommand(
                requestBody,
                messageId,
                messageTimestamp,
                messageSignature,
                messageType
            );

            var challenge = await _mediator.Send(command, cancellationToken);

            // If it's a challenge verification, return the challenge
            if (!string.IsNullOrEmpty(challenge))
            {
                return Content(challenge, "text/plain");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Twitch EventSub callback");
            return StatusCode(500, "Internal server error");
        }
    }
}
