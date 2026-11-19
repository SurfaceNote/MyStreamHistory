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
            var response = await RestoreAppExceptions(() => bus.Request<TRequest, EmptyResponse>(request, cancellationToken));
            return response.Message;
        }

        public async Task<TransportResponse<TResponse>> SendRequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : class
            where TResponse : class
        {
            var response = await RestoreAppExceptions(() => bus.Request<TRequest, TResponse>(request, cancellationToken));
            return new TransportResponse<TResponse> { Success = response.Message };
        }

        public async Task<TransportResponse<TSuccessResponse, TFailureResponse>> SendRequestAsync<TRequest, TSuccessResponse, TFailureResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : class
            where TSuccessResponse : class
            where TFailureResponse : class
        {
            var client = bus.CreateRequestClient<TRequest>();

            var response = await RestoreAppExceptions(() => client.GetResponse<TSuccessResponse, TFailureResponse>(request, cancellationToken));

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

        /// <summary>
        /// Turns a fault caused by <see cref="AppException"/> in the consumer back into that exception, so a
        /// business error (expired token, missing user) keeps its status code instead of becoming HTTP 500.
        /// </summary>
        private static async Task<T> RestoreAppExceptions<T>(Func<Task<T>> request)
        {
            try
            {
                return await request();
            }
            catch (RequestFaultException exception) when (FindErrorCode(exception.Fault) is { } errorCode)
            {
                throw new AppException(errorCode);
            }
        }

        private static string? FindErrorCode(Fault? fault) =>
            fault?.Exceptions
                .Select(info => info.Data is { } data && data.TryGetValue(AppException.ErrorCodeDataKey, out var code)
                    ? code?.ToString()
                    : null)
                .FirstOrDefault(code => !string.IsNullOrEmpty(code));
    }
}
