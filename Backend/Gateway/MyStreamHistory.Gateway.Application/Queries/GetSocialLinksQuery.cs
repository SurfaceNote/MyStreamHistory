using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;

namespace MyStreamHistory.Gateway.Application.Queries;

public record GetSocialLinksQuery(Guid UserId) : IRequest<GetSocialLinksResponseDto>;

