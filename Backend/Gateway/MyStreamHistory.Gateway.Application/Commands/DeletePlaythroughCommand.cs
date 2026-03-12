using MediatR;

namespace MyStreamHistory.Gateway.Application.Commands;

public record DeletePlaythroughCommand(int TwitchUserId, Guid PlaythroughId) : IRequest;
