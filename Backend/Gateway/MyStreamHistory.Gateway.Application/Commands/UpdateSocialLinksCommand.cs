using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;

namespace MyStreamHistory.Gateway.Application.Commands;

public record UpdateSocialLinksCommand(Guid UserId, SocialLinkDto SocialLink) 
    : IRequest<UpdateSocialLinksResponseDto>;

