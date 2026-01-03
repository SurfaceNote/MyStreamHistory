using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;
using MyStreamHistory.Shared.Base.Contracts.Auth.Responses;
using MyStreamHistory.Shared.Base.Error;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Commands;

public class RefreshTokenCommandHandler(ITransportBus bus) : IRequestHandler<RefreshTokenCommand, RefreshTokenResultDto> 
{
    public async Task<RefreshTokenResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.Token))
        {
            throw new AppException(ErrorCodes.InvalidCredentials);
        }

        var response =
            await bus
                .SendRequestAsync<RefreshTokenRequestContract, RefreshTokenResponseContract,
                    BaseFailedResponseContract>(new RefreshTokenRequestContract
                {
                    Token = request.Token
                });

        if (response.IsSuccess)
        {
            return new RefreshTokenResultDto
            {
                AccessToken = response.Success!.AccessToken,
                RefreshToken = response.Success!.RefreshToken
            };
        }

        if (response.IsFailure)
        {
            throw new AppException(response.Failure!.Reason);
        }

        throw new InvalidOperationException("Refresh token failed");
    }
}