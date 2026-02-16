using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;

namespace MyStreamHistory.Gateway.Application.Queries;

public record GetSocialLinksByTwitchIdQuery(int TwitchId) : IRequest<GetSocialLinksResponseDto>;

