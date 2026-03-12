using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;

namespace MyStreamHistory.Gateway.Application.Commands;

public record UpsertPlaythroughCommand(int TwitchUserId, UpsertPlaythroughRequestDto Request) : IRequest<PlaythroughDto>;
