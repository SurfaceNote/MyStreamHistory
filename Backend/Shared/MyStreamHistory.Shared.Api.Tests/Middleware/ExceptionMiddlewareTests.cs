using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using MyStreamHistory.Shared.Api.Middleware;
using MyStreamHistory.Shared.Api.Wrappers;
using MyStreamHistory.Shared.Base.Error;
using MyStreamHistory.Shared.Base.Exceptions;
using Xunit;

namespace MyStreamHistory.Shared.Api.Tests.Middleware;

public class ExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AppException_ReturnsMappedStatusAndApiResult()
    {
        const string correlationId = "test-correlation-id";
        var context = CreateContext(correlationId);
        var middleware = new ExceptionMiddleware(
            _ => throw new AppException(ErrorCodes.InvalidCredentials),
            NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        var response = await ReadResponse(context);

        Assert.Equal((int)HttpStatusCode.Unauthorized, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);
        Assert.False(response.Success);
        Assert.Equal([ErrorCodes.InvalidCredentials], response.Errors);
        Assert.Equal(correlationId, response.Meta.CorrelationId);
    }

    [Fact]
    public async Task InvokeAsync_UnknownException_ReturnsInternalErrorApiResult()
    {
        const string correlationId = "test-correlation-id";
        var context = CreateContext(correlationId);
        var middleware = new ExceptionMiddleware(
            _ => throw new InvalidOperationException("boom"),
            NullLogger<ExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        var response = await ReadResponse(context);

        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
        Assert.StartsWith("application/json", context.Response.ContentType);
        Assert.False(response.Success);
        Assert.Equal([ErrorCodes.InternalError], response.Errors);
        Assert.Equal(correlationId, response.Meta.CorrelationId);
    }

    private static DefaultHttpContext CreateContext(string traceIdentifier)
    {
        return new DefaultHttpContext
        {
            TraceIdentifier = traceIdentifier,
            Response =
            {
                Body = new MemoryStream()
            }
        };
    }

    private static async Task<ApiResultContainer> ReadResponse(HttpContext context)
    {
        context.Response.Body.Position = 0;

        var response = await JsonSerializer.DeserializeAsync<ApiResultContainer>(
            context.Response.Body,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return response ?? throw new InvalidOperationException("Response body is empty.");
    }
}
