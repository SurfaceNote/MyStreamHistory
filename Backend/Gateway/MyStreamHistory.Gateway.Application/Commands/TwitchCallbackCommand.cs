using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;

namespace MyStreamHistory.Gateway.Application.Commands
{
    public record TwitchCallbackCommand(string Code, string State) : IRequest<TwitchCallbackResultDto>;
}
