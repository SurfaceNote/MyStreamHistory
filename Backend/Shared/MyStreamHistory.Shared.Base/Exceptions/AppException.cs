namespace MyStreamHistory.Shared.Base.Exceptions
{
    public class AppException(string errorCode, string? message = null) : Exception(message ?? errorCode)
    {
        public string ErrorCode { get; } = errorCode;
    }
}
