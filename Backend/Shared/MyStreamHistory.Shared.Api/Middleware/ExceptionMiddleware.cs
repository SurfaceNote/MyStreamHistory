using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Api.Wrappers;
using MyStreamHistory.Shared.Base.Error;

namespace MyStreamHistory.Shared.Api.Middleware
{
    public class ExceptionMiddleware (RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await next(context);
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
