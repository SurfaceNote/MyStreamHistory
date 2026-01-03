using Microsoft.AspNetCore.Builder;
using MyStreamHistory.Shared.Api.Middleware;

namespace MyStreamHistory.Shared.Api.Extensions
{
    public static class AppExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseAppExceptionHandler(this IApplicationBuilder app)
        {
            return app.UseMiddleware<AppExceptionMiddleware>();
        }
    }
}
