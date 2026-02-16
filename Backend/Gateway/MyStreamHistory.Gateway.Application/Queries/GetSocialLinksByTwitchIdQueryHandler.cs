using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;
using MyStreamHistory.Shared.Base.Contracts.Auth.Responses;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetSocialLinksByTwitchIdQueryHandler(ITransportBus bus) 
    : IRequestHandler<GetSocialLinksByTwitchIdQuery, GetSocialLinksResponseDto>
{
    public async Task<GetSocialLinksResponseDto> Handle(GetSocialLinksByTwitchIdQuery request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            GetSocialLinksByTwitchIdRequestContract, GetSocialLinksResponseContract, BaseFailedResponseContract>
        (
            new GetSocialLinksByTwitchIdRequestContract { TwitchId = request.TwitchId }, cancellationToken
        );

        if (response.IsSuccess)
        {
            return new GetSocialLinksResponseDto
            {
                SocialLinks = response.Success!.SocialLinks.Select(sl => new SocialLinkDto
                {
                    SocialNetworkType = sl.SocialNetworkType,
                    Path = sl.Path,
                    FullUrl = sl.FullUrl
                }).ToList()
            };
        }

        if (response.IsFailure)
        {
            throw new AppException(response.Failure!.Reason);
        }

        throw new InvalidOperationException();
    }
}

