using MassTransit;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Shared.Infrastructure.Transport
{
    public class TransportBus(IBus bus) : ITransportBus
    {
        public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            await bus.Publish(message, cancellationToken);
        }

        public async Task<EmptyResponse> SendRequestAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : class
        {
            var response = await bus.Request<TRequest, EmptyResponse>(request, cancellationToken);
            return response.Message;
        }

        public async Task<TransportResponse<TResponse>> SendRequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : class
            where TResponse : class
        {
            var response = await bus.Request<TRequest, TResponse>(request, cancellationToken);
            return new TransportResponse<TResponse> { Success = response.Message };
        }

        public async Task<TransportResponse<TSuccessResponse, TFailureResponse>> SendRequestAsync<TRequest, TSuccessResponse, TFailureResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : class
            where TSuccessResponse : class
            where TFailureResponse : class
        {
            var client = bus.CreateRequestClient<TRequest>();

            var response = await client.GetResponse<TSuccessResponse, TFailureResponse>(request, cancellationToken);

            if (response.Is(out Response<TSuccessResponse>? success))
            {
                return new TransportResponse<TSuccessResponse, TFailureResponse>
                {
                    Success = success.Message
                };
            }

            if (response.Is(out Response<TFailureResponse>? failure))
            {
                return new TransportResponse<TSuccessResponse, TFailureResponse>
                {
                    Failure = failure.Message
                };
            }

            throw new TransportBusException("Unexpected response type");
        }
    }
}
