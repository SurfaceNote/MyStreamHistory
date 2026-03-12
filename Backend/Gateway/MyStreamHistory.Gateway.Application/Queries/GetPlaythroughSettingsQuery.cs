using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;

namespace MyStreamHistory.Gateway.Application.Queries;

public record GetPlaythroughSettingsQuery(int TwitchUserId) : IRequest<PlaythroughSettingsDto>;
