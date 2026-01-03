using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;
using MyStreamHistory.Shared.Base.Contracts.Auth.Responses;
using MyStreamHistory.Shared.Base.Error;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Commands
{
    public class TwitchCallbackCommandHandler(ITransportBus bus) : IRequestHandler<TwitchCallbackCommand, TwitchCallbackResultDto>
    {
        public async Task<TwitchCallbackResultDto> Handle(TwitchCallbackCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(request.Code) || string.IsNullOrEmpty(request.State))
            {
                throw new AppException(ErrorCodes.InvalidCredentials); 
            }

            var response =
                await bus
                    .SendRequestAsync<TwitchAuthorizeRequestContract, TwitchAuthorizeResponseContract,
                        BaseFailedResponseContract>(
                        new TwitchAuthorizeRequestContract
                        {
                            Code = request.Code,
                            State = request.State
                        });

            if (response.IsSuccess)
            {
                return new TwitchCallbackResultDto
                {
                    AccessToken = response.Success!.AccessToken,
                    RefreshToken = response.Success!.RefreshToken
                };
            }

            if (response.IsFailure)
            {
                throw new AppException(response.Failure!.Reason);
            }

            throw new InvalidOperationException("Twitch authorization failed");
        }
    }
}
