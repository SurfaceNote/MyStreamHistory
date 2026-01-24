using MediatR;

namespace MyStreamHistory.Gateway.Application.Commands.TwitchEventSub;

public record ProcessEventSubCommand(string RequestBody, string MessageId, string MessageTimestamp, string MessageSignature, string MessageType) : IRequest<string?>;

