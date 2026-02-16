using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;
using MyStreamHistory.Shared.Base.Contracts.Auth.Responses;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetSocialLinksQueryHandler(ITransportBus bus) 
    : IRequestHandler<GetSocialLinksQuery, GetSocialLinksResponseDto>
{
    public async Task<GetSocialLinksResponseDto> Handle(GetSocialLinksQuery request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            GetSocialLinksRequestContract, GetSocialLinksResponseContract, BaseFailedResponseContract>
        (
            new GetSocialLinksRequestContract { UserId = request.UserId }, cancellationToken
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

