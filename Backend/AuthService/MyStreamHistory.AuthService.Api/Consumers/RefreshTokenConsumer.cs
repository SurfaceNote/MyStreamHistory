using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;

namespace MyStreamHistory.AuthService.Api.Consumers;

public class RefreshTokenConsumer : IConsumer<RefreshTokenRequestContract>
{
    // TODO: Continue working on refresh token
    public Task Consume(ConsumeContext<RefreshTokenRequestContract> context)
    {
        throw new NotImplementedException();
    }
}