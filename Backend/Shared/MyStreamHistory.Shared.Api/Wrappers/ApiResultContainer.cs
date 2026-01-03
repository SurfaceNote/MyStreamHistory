namespace MyStreamHistory.Shared.Api.Wrappers
{
    public class ApiResultContainer<T>
    {
        public bool Success { get; init; }
        public T? Data { get; init; }
        public List<string>? Errors { get; init; }
        public MetaData Meta { get; init; } = new();

        public class MetaData
        {
            public DateTime Timestamp { get; init; } = DateTime.UtcNow;
            public string? CorrelationId { get; init; }
        }
    }


    public class ApiResultContainer
    {
        public bool Success { get; init; }
        public List<string>? Errors { get; init; }
        public ApiResultContainer<object>.MetaData Meta { get; init; } = new();
    }
}
