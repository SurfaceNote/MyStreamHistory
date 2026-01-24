using MediatR;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;

namespace MyStreamHistory.Gateway.Application.Queries;

/// <summary>
/// Query to get all EventSub subscriptions
/// </summary>
public record GetEventSubSubscriptionsQuery : IRequest<EventSubSubscriptionsDto>;

