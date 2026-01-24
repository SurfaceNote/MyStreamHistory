using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;

namespace MyStreamHistory.AuthService.Api.Consumers;

public class RefreshTokenConsumer : IConsumer<RefreshTokenRequestContract>
{
    // TODO: Продолжить работать над рефреш токеном
    public Task Consume(ConsumeContext<RefreshTokenRequestContract> context)
    {
        throw new NotImplementedException();
    }
}