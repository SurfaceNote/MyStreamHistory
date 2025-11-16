namespace MyStreamHistory.Shared.Application.Transport
{
    public interface ITransportBus
    {
        Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;

        Task<EmptyResponse> SendRequestAsync<TRequest>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : class;

        Task<TransportResponse<TResponse>> SendRequestAsync<TRequest, TResponse>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : class
            where TResponse : class;

        Task<TransportResponse<TSuccessResponse, TFailureResponse>> SendRequestAsync<TRequest, TSuccessResponse, TFailureResponse>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : class
            where TSuccessResponse : class
            where TFailureResponse : class;
    }
}
