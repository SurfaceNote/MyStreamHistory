using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Shared.Api.DTOs;

namespace MyStreamHistory.Gateway.Application.Commands;

public record RefreshTokenCommand(Guid UserId, string Token) : IRequest<RefreshTokenResultDto>;