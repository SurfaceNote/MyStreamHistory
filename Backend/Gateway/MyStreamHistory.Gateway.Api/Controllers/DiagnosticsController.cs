using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyStreamHistory.Gateway.Application.Commands;
using MyStreamHistory.Gateway.Application.Queries;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Api.Wrappers;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;

namespace MyStreamHistory.Gateway.Api.Controllers;

/// <summary>
/// Diagnostic endpoints for monitoring system health and EventSub subscriptions
/// </summary>
[ApiController]
[Route("diagnostics")]
public class DiagnosticsController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(IMediator mediator, ILogger<DiagnosticsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Get all current EventSub subscriptions.
    /// TEMPORARY: Only accessible by user "kination" during development.
    /// This restriction will be replaced with proper role-based authorization in production.
    /// </summary>
    /// <returns>List of all active EventSub subscriptions with details</returns>
    [HttpGet("eventsub-subscriptions")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResultContainer<EventSubSubscriptionsDto>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 401)]
    [ProducesResponseType(typeof(ApiResultContainer), 403)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<EventSubSubscriptionsDto>>> GetEventSubSubscriptions(
        CancellationToken cancellationToken)
    {
        // TEMPORARY: Hardcoded username check for development only
        // TODO: Replace with proper admin role check in production
        var userName = User.FindFirstValue("UserName");
        
        if (string.IsNullOrEmpty(userName))
        {
            _logger.LogWarning("Unauthorized access attempt to eventsub-subscriptions: UserName claim not found");
            return Unauthorized("Invalid authentication token: UserName claim not found");
        }

        if (userName != "kination")
        {
            _logger.LogWarning("Access denied to eventsub-subscriptions for user: {UserName}", userName);
            return StatusCode(403, new 
            { 
                message = "Access denied. This diagnostic endpoint is restricted during development.",
                note = "This is a temporary restriction while the application is in development.",
                userName
            });
        }

        try
        {
            var query = new GetEventSubSubscriptionsQuery();
            var subscriptions = await _mediator.Send(query, cancellationToken);

            _logger.LogInformation("User {UserName} accessed EventSub subscriptions. Total: {Total}, Cost: {Cost}/{MaxCost}", 
                userName, subscriptions.Total, subscriptions.TotalCost, subscriptions.MaxTotalCost);

            return this.Success(subscriptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while fetching EventSub subscriptions for user {UserName}", userName);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    /// <summary>
    /// Delete all EventSub subscriptions.
    /// TEMPORARY: Only accessible by user "kination" during development.
    /// WARNING: This will unsubscribe from ALL events for ALL users!
    /// </summary>
    /// <returns>Number of deleted subscriptions</returns>
    [HttpDelete("eventsub-subscriptions")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResultContainer<DeleteAllSubscriptionsResponseContract>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 401)]
    [ProducesResponseType(typeof(ApiResultContainer), 403)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<DeleteAllSubscriptionsResponseContract>>> DeleteAllSubscriptions(
        CancellationToken cancellationToken)
    {
        // TEMPORARY: Hardcoded username check for development only
        // TODO: Replace with proper admin role check in production
        var userName = User.FindFirstValue("UserName");
        
        if (string.IsNullOrEmpty(userName))
        {
            _logger.LogWarning("Unauthorized access attempt to delete-all-subscriptions: UserName claim not found");
            return Unauthorized("Invalid authentication token: UserName claim not found");
        }

        if (userName != "kination")
        {
            _logger.LogWarning("Access denied to delete-all-subscriptions for user: {UserName}", userName);
            return StatusCode(403, new 
            { 
                message = "Access denied. This diagnostic endpoint is restricted during development.",
                note = "This is a temporary restriction while the application is in development.",
                userName
            });
        }

        try
        {
            _logger.LogWarning("User {UserName} is deleting ALL EventSub subscriptions", userName);
            
            var command = new DeleteAllSubscriptionsCommand();
            var result = await _mediator.Send(command, cancellationToken);

            _logger.LogWarning("User {UserName} deleted {DeletedCount} EventSub subscriptions", 
                userName, result.DeletedCount);

            return this.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while deleting all subscriptions for user {UserName}", userName);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    /// <summary>
    /// Subscribe to all registered users (stream.online and stream.offline events).
    /// TEMPORARY: Only accessible by user "kination" during development.
    /// This will create subscriptions for ALL users in the system.
    /// </summary>
    /// <returns>Subscription results with success/fail counts</returns>
    [HttpPost("eventsub-subscriptions/subscribe-all")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResultContainer<SubscribeToAllUsersResponseContract>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 401)]
    [ProducesResponseType(typeof(ApiResultContainer), 403)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<SubscribeToAllUsersResponseContract>>> SubscribeToAllUsers(
        CancellationToken cancellationToken)
    {
        // TEMPORARY: Hardcoded username check for development only
        // TODO: Replace with proper admin role check in production
        var userName = User.FindFirstValue("UserName");
        
        if (string.IsNullOrEmpty(userName))
        {
            _logger.LogWarning("Unauthorized access attempt to subscribe-all: UserName claim not found");
            return Unauthorized("Invalid authentication token: UserName claim not found");
        }

        if (userName != "kination")
        {
            _logger.LogWarning("Access denied to subscribe-all for user: {UserName}", userName);
            return StatusCode(403, new 
            { 
                message = "Access denied. This diagnostic endpoint is restricted during development.",
                note = "This is a temporary restriction while the application is in development.",
                userName
            });
        }

        try
        {
            _logger.LogInformation("User {UserName} is subscribing to ALL users", userName);
            
            var command = new SubscribeToAllUsersCommand();
            var result = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation("User {UserName} subscribed to {UserCount} users. Success: {SuccessCount}, Failed: {FailCount}", 
                userName, result.UserCount, result.SuccessCount, result.FailCount);

            return this.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while subscribing to all users for user {UserName}", userName);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    /// <summary>
    /// Get all chat message EventSub subscriptions from ViewerService.
    /// TEMPORARY: Only accessible by user "kination" during development.
    /// This shows subscriptions to channel.chat.message events.
    /// </summary>
    /// <returns>List of all chat message subscriptions</returns>
    [HttpGet("chat-subscriptions")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResultContainer<GetChatSubscriptionsResponseContract>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 401)]
    [ProducesResponseType(typeof(ApiResultContainer), 403)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<GetChatSubscriptionsResponseContract>>> GetChatSubscriptions(
        CancellationToken cancellationToken)
    {
        // TEMPORARY: Hardcoded username check for development only
        // TODO: Replace with proper admin role check in production
        var userName = User.FindFirstValue("UserName");
        
        if (string.IsNullOrEmpty(userName))
        {
            _logger.LogWarning("Unauthorized access attempt to chat-subscriptions: UserName claim not found");
            return Unauthorized("Invalid authentication token: UserName claim not found");
        }

        if (userName != "kination")
        {
            _logger.LogWarning("Access denied to chat-subscriptions for user: {UserName}", userName);
            return StatusCode(403, new 
            { 
                message = "Access denied. This diagnostic endpoint is restricted during development.",
                note = "This is a temporary restriction while the application is in development.",
                userName
            });
        }

        try
        {
            var query = new GetChatSubscriptionsQuery();
            var subscriptions = await _mediator.Send(query, cancellationToken);

            _logger.LogInformation("User {UserName} accessed chat subscriptions. Count: {Count}", 
                userName, subscriptions.Count);

            return this.Success(subscriptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while fetching chat subscriptions for user {UserName}", userName);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    /// <summary>
    /// Cleanup all chat message EventSub subscriptions from ViewerService.
    /// TEMPORARY: Only accessible by user "kination" during development.
    /// WARNING: This will unsubscribe from ALL channel.chat.message events!
    /// </summary>
    /// <returns>Number of deleted subscriptions</returns>
    [HttpDelete("chat-subscriptions")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResultContainer<CleanupChatSubscriptionsResponseContract>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 401)]
    [ProducesResponseType(typeof(ApiResultContainer), 403)]
    [ProducesResponseType(typeof(ApiResultContainer), 500)]
    public async Task<ActionResult<ApiResultContainer<CleanupChatSubscriptionsResponseContract>>> CleanupChatSubscriptions(
        CancellationToken cancellationToken)
    {
        // TEMPORARY: Hardcoded username check for development only
        // TODO: Replace with proper admin role check in production
        var userName = User.FindFirstValue("UserName");
        
        if (string.IsNullOrEmpty(userName))
        {
            _logger.LogWarning("Unauthorized access attempt to cleanup-chat-subscriptions: UserName claim not found");
            return Unauthorized("Invalid authentication token: UserName claim not found");
        }

        if (userName != "kination")
        {
            _logger.LogWarning("Access denied to cleanup-chat-subscriptions for user: {UserName}", userName);
            return StatusCode(403, new 
            { 
                message = "Access denied. This diagnostic endpoint is restricted during development.",
                note = "This is a temporary restriction while the application is in development.",
                userName
            });
        }

        try
        {
            _logger.LogWarning("User {UserName} is cleaning up ALL chat subscriptions", userName);
            
            var command = new CleanupChatSubscriptionsCommand();
            var result = await _mediator.Send(command, cancellationToken);

            _logger.LogWarning("User {UserName} cleaned up {DeletedCount} chat subscriptions", 
                userName, result.DeletedCount);

            return this.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while cleaning up chat subscriptions for user {UserName}", userName);
            return StatusCode(500, "An error occurred while processing your request");
        }
    }
}

