namespace MyStreamHistory.Shared.Application.Transport
{
    public sealed class EmptyResponse
    {
        public static readonly EmptyResponse Instance = new();
        private EmptyResponse() { }
    }
}
