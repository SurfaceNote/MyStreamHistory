using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Api.Mapping;
using MyStreamHistory.Shared.Api.Wrappers;
using MyStreamHistory.Shared.Base.Error;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Shared.Api.Middleware
{
    public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch (AppException ex)
            {
                logger.LogError(ex, "Application exception handled: {Message}", ex.Message);

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
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Unhandled exception: {Message}", ex.Message);

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                var response = new ApiResultContainer
                {
                    Success = false,
                    Errors = new List<string> { ErrorCodes.InternalError },
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
