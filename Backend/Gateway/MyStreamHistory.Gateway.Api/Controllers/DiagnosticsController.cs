using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MyStreamHistory.Gateway.Application.Commands;
using MyStreamHistory.Gateway.Application.Queries;
using MyStreamHistory.Shared.Api.Authorization;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Api.Wrappers;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;

namespace MyStreamHistory.Gateway.Api.Controllers;

/// <summary>
/// Diagnostic endpoints for monitoring system health and EventSub subscriptions
/// </summary>
[ApiController]
[Route("diagnostics")]
[Authorize(Policy = PolicyNames.DiagnosticsAdmin)]
[EnableRateLimiting(PolicyNames.DiagnosticsAdmin)]
public class DiagnosticsController : ApiControllerBase
{
    private const string ConfirmationHeaderName = "X-Diagnostics-Confirmation";
    private const string DeleteAllEventSubSubscriptionsConfirmation = "delete-all-eventsub-subscriptions";
    private const string CleanupAllChatSubscriptionsConfirmation = "cleanup-all-chat-subscriptions";

    private readonly IMediator _mediator;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(IMediator mediator, ILogger<DiagnosticsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all current EventSub subscriptions.
    /// </summary>
    /// <returns>List of all active EventSub subscriptions with details</returns>
    [HttpGet("eventsub-subscriptions")]
    [ProducesResponseType(typeof(ApiResultContainer<EventSubSubscriptionsDto>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 401)]
    [ProducesResponseType(typeof(ApiResultContainer), 403)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<EventSubSubscriptionsDto>>> GetEventSubSubscriptions(
        CancellationToken cancellationToken)
    {
        try
        {
            var user = GetAuditUser();
            var query = new GetEventSubSubscriptionsQuery();
            var subscriptions = await _mediator.Send(query, cancellationToken);

            _logger.LogInformation("Diagnostics admin {UserId} accessed EventSub subscriptions. Total: {Total}, Cost: {Cost}/{MaxCost}", 
                user.UserId, subscriptions.Total, subscriptions.TotalCost, subscriptions.MaxTotalCost);

            return this.Success(subscriptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while fetching EventSub subscriptions for diagnostics admin {UserId}", GetAuditUser().UserId);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    /// <summary>
    /// Delete all EventSub subscriptions.
    /// WARNING: This will unsubscribe from ALL events for ALL users!
    /// </summary>
    /// <returns>Number of deleted subscriptions</returns>
    [HttpDelete("eventsub-subscriptions")]
    [ProducesResponseType(typeof(ApiResultContainer<DeleteAllSubscriptionsResponseContract>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 400)]
    [ProducesResponseType(typeof(ApiResultContainer), 401)]
    [ProducesResponseType(typeof(ApiResultContainer), 403)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<DeleteAllSubscriptionsResponseContract>>> DeleteAllSubscriptions(
        CancellationToken cancellationToken)
    {
        if (!HasConfirmation(DeleteAllEventSubSubscriptionsConfirmation))
        {
            return MissingConfirmation(DeleteAllEventSubSubscriptionsConfirmation);
        }

        try
        {
            var user = GetAuditUser();
            LogDestructiveAudit("DeleteAllEventSubSubscriptions", user);
            
            var command = new DeleteAllSubscriptionsCommand();
            var result = await _mediator.Send(command, cancellationToken);

            _logger.LogWarning("Diagnostics admin {UserId} deleted {DeletedCount} EventSub subscriptions. CorrelationId: {CorrelationId}", 
                user.UserId, result.DeletedCount, GetCorrelationId());

            return this.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while deleting all EventSub subscriptions for diagnostics admin {UserId}", GetAuditUser().UserId);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    /// <summary>
    /// Subscribe to all registered users (stream.online and stream.offline events).
    /// This will create subscriptions for ALL users in the system.
    /// </summary>
    /// <returns>Subscription results with success/fail counts</returns>
    [HttpPost("eventsub-subscriptions/subscribe-all")]
    [ProducesResponseType(typeof(ApiResultContainer<SubscribeToAllUsersResponseContract>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 401)]
    [ProducesResponseType(typeof(ApiResultContainer), 403)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<SubscribeToAllUsersResponseContract>>> SubscribeToAllUsers(
        CancellationToken cancellationToken)
    {
        try
        {
            var user = GetAuditUser();
            _logger.LogInformation("Diagnostics admin {UserId} is subscribing to ALL users", user.UserId);
            
            var command = new SubscribeToAllUsersCommand();
            var result = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation("Diagnostics admin {UserId} subscribed to {UserCount} users. Success: {SuccessCount}, Failed: {FailCount}", 
                user.UserId, result.UserCount, result.SuccessCount, result.FailCount);

            return this.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while subscribing to all users for diagnostics admin {UserId}", GetAuditUser().UserId);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    /// <summary>
    /// Get all chat message EventSub subscriptions from ViewerService.
    /// This shows subscriptions to channel.chat.message events.
    /// </summary>
    /// <returns>List of all chat message subscriptions</returns>
    [HttpGet("chat-subscriptions")]
    [ProducesResponseType(typeof(ApiResultContainer<GetChatSubscriptionsResponseContract>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 401)]
    [ProducesResponseType(typeof(ApiResultContainer), 403)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<GetChatSubscriptionsResponseContract>>> GetChatSubscriptions(
        CancellationToken cancellationToken)
    {
        try
        {
            var user = GetAuditUser();
            var query = new GetChatSubscriptionsQuery();
            var subscriptions = await _mediator.Send(query, cancellationToken);

            _logger.LogInformation("Diagnostics admin {UserId} accessed chat subscriptions. Count: {Count}", 
                user.UserId, subscriptions.Count);

            return this.Success(subscriptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while fetching chat subscriptions for diagnostics admin {UserId}", GetAuditUser().UserId);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    /// <summary>
    /// Cleanup all chat message EventSub subscriptions from ViewerService.
    /// WARNING: This will unsubscribe from ALL channel.chat.message events!
    /// </summary>
    /// <returns>Number of deleted subscriptions</returns>
    [HttpDelete("chat-subscriptions")]
    [ProducesResponseType(typeof(ApiResultContainer<CleanupChatSubscriptionsResponseContract>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 400)]
    [ProducesResponseType(typeof(ApiResultContainer), 401)]
    [ProducesResponseType(typeof(ApiResultContainer), 403)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<CleanupChatSubscriptionsResponseContract>>> CleanupChatSubscriptions(
        CancellationToken cancellationToken)
    {
        if (!HasConfirmation(CleanupAllChatSubscriptionsConfirmation))
        {
            return MissingConfirmation(CleanupAllChatSubscriptionsConfirmation);
        }

        try
        {
            var user = GetAuditUser();
            LogDestructiveAudit("CleanupAllChatSubscriptions", user);
            
            var command = new CleanupChatSubscriptionsCommand();
            var result = await _mediator.Send(command, cancellationToken);

            _logger.LogWarning("Diagnostics admin {UserId} cleaned up {DeletedCount} chat subscriptions. CorrelationId: {CorrelationId}", 
                user.UserId, result.DeletedCount, GetCorrelationId());

            return this.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while cleaning up chat subscriptions for diagnostics admin {UserId}", GetAuditUser().UserId);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    private bool HasConfirmation(string expectedValue)
    {
        return Request.Headers.TryGetValue(ConfirmationHeaderName, out var values)
               && values.Any(value => string.Equals(value, expectedValue, StringComparison.Ordinal));
    }

    private BadRequestObjectResult MissingConfirmation(string expectedValue)
    {
        return BadRequest(new
        {
            message = "Destructive diagnostics action requires explicit confirmation.",
            requiredHeader = ConfirmationHeaderName,
            requiredValue = expectedValue
        });
    }

    private void LogDestructiveAudit(string action, AuditUser user)
    {
        _logger.LogWarning(
            "AUDIT Diagnostics destructive action {Action} requested by UserId {UserId}, TwitchId {TwitchId}, UserName {UserName}, CorrelationId {CorrelationId}, RemoteIp {RemoteIp}",
            action,
            user.UserId,
            user.TwitchId,
            user.UserName,
            GetCorrelationId(),
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }

    private string GetCorrelationId()
    {
        return Request.Headers.TryGetValue("X-Correlation-ID", out var values)
               && !string.IsNullOrWhiteSpace(values.FirstOrDefault())
            ? values.First()!
            : HttpContext.TraceIdentifier;
    }

    private AuditUser GetAuditUser()
    {
        return new AuditUser(
            User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
            User.FindFirstValue("TwitchId") ?? "unknown",
            User.FindFirstValue("UserName") ?? "unknown");
    }

    private sealed record AuditUser(string UserId, string TwitchId, string UserName);
}

