using MediatR;
using MyStreamHistory.Shared.Base.Contracts.Diagnostics.Responses;

namespace MyStreamHistory.Gateway.Application.Commands;

/// <summary>
/// Command to subscribe to all registered users
/// </summary>
public record SubscribeToAllUsersCommand : IRequest<SubscribeToAllUsersResponseContract>;

