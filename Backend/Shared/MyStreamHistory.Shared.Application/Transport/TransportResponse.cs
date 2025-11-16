namespace MyStreamHistory.Shared.Application.Transport
{
    public class TransportResponse<TSuccess, TFailure>
        where TSuccess : class
        where TFailure : class
    {
        public TSuccess? Success { get; init; }
        public TFailure? Failure { get; init; }

        public bool IsSuccess => Success is not null;
        public bool IsFailure => Failure is not null;
    }

    public class TransportResponse<TResponse>
        where TResponse : class
    {
        public TResponse? Success { get; init; }
        public bool IsSuccess => Success is not null;
    }
}
