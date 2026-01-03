using Microsoft.AspNetCore.Builder;
using MyStreamHistory.Shared.Api.Middleware;

namespace MyStreamHistory.Shared.Api.Extensions;

public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionMiddleware>();
    }
}