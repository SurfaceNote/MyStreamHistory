using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;
using MyStreamHistory.Shared.Base.Contracts.Auth.Responses;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Commands;

public class UpdateSocialLinksCommandHandler(ITransportBus bus) 
    : IRequestHandler<UpdateSocialLinksCommand, UpdateSocialLinksResponseDto>
{
    public async Task<UpdateSocialLinksResponseDto> Handle(UpdateSocialLinksCommand request, CancellationToken cancellationToken)
    {
        var contractLinks = request.SocialLinks.Select(sl => new Shared.Base.Contracts.Auth.SocialLinkDto
        {
            SocialNetworkType = sl.SocialNetworkType,
            Path = sl.Path,
            FullUrl = sl.FullUrl
        }).ToList();

        var response = await bus.SendRequestAsync<
            UpdateSocialLinksRequestContract, 
            UpdateSocialLinksResponseContract,
            BaseFailedResponseContract>
        (
            new UpdateSocialLinksRequestContract 
            { 
                UserId = request.UserId,
                SocialLinks = contractLinks
            }, 
            cancellationToken
        );

        if (response.IsSuccess)
        {
            return new UpdateSocialLinksResponseDto
            {
                Success = response.Success!.Success,
                Error = response.Success!.Error
            };
        }

        if (response.IsFailure)
        {
            return new UpdateSocialLinksResponseDto
            {
                Success = false,
                Error = response.Failure!.Reason
            };
        }

        throw new InvalidOperationException("Update social links failed");
    }
}
