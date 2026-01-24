using MediatR;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;

namespace MyStreamHistory.Gateway.Application.Commands;

/// <summary>
/// Command to delete all EventSub subscriptions
/// </summary>
public record DeleteAllSubscriptionsCommand : IRequest<DeleteAllSubscriptionsResponseContract>;

