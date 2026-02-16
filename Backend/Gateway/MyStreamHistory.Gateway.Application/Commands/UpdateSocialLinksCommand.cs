using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;

namespace MyStreamHistory.Gateway.Application.Commands;

public record UpdateSocialLinksCommand(Guid UserId, List<SocialLinkDto> SocialLinks) 
    : IRequest<UpdateSocialLinksResponseDto>;

