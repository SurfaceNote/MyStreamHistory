using MediatR;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;

namespace MyStreamHistory.Gateway.Application.Commands;

/// <summary>
/// Command to cleanup all chat message EventSub subscriptions
/// </summary>
public record CleanupChatSubscriptionsCommand : IRequest<CleanupChatSubscriptionsResponseContract>;

