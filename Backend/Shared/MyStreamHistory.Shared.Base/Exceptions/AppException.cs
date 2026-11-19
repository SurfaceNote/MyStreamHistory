namespace MyStreamHistory.Shared.Base.Exceptions
{
    public class AppException : Exception
    {
        /// <summary>
        /// Key of the error code in <see cref="Exception.Data"/>. MassTransit copies <c>Data</c> into a fault,
        /// so the caller of a failed bus request can restore the original error code.
        /// </summary>
        public const string ErrorCodeDataKey = "AppErrorCode";

        public AppException(string errorCode, string? message = null) : base(message ?? errorCode)
        {
            ErrorCode = errorCode;
            Data[ErrorCodeDataKey] = errorCode;
        }

        public string ErrorCode { get; }
    }
}
