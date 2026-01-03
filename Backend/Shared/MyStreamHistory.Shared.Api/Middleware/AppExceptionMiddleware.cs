using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Api.Mapping;
using MyStreamHistory.Shared.Api.Wrappers;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Shared.Api.Middleware
{
    public class AppExceptionMiddleware(RequestDelegate next, ILogger<AppExceptionMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch (AppException ex)
            {
                logger.LogError(ex, "ApplicationException handled: {Message}", ex.Message);

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = ErrorStatusMapper.GetStatusCode(ex.ErrorCode);

                var response = new ApiResultContainer
                {
                    Success = false,
                    Errors = new List<string> { ex.ErrorCode },
                    Meta = new ApiResultContainer<object>.MetaData
                    {
                        Timestamp = DateTime.UtcNow,
                        CorrelationId = context.TraceIdentifier
                    }
                };

                await context.Response.WriteAsJsonAsync(response);
            }
        }

    }
}
