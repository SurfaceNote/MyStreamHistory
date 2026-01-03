using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Api.Wrappers;
using System.Net.WebSockets;

namespace MyStreamHistory.Shared.Api.Middleware
{
    public class ValidationExceptionMiddleware(RequestDelegate next, ILogger<ValidationExceptionMiddleware> logger)
    {
        public async Task Invoke(HttpContext context)
        {
            try
            {
                await next(context);
            }
            catch(FluentValidation.ValidationException ex)
            {
                logger.LogError("Validation error: {Errors}", ex.Errors);

                context.Response.StatusCode = 400;
                context.Response.ContentType = "application/json";

                var response = new ApiResultContainer
                {
                    Success = false,
                    Errors = ex.Errors.Select(e => $"{e.PropertyName}:{e.ErrorMessage}").ToList(),
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
