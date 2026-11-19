using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Error;
using MyStreamHistory.Shared.Base.Exceptions;
using MyStreamHistory.Shared.Infrastructure.Transport;
using Xunit;

namespace MyStreamHistory.Shared.Api.Tests.Transport;

/// <summary>
/// A business error thrown by a consumer must reach the caller with its error code: no retries,
/// no fault, and no HTTP 500 in Gateway.
/// </summary>
public class BusBusinessErrorTests : IAsyncLifetime
{
    private readonly Attempts _attempts = new();
    private ServiceProvider _provider = null!;
    private TransportBus _bus = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_attempts);
        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<BusinessErrorConsumer>();
            bus.AddConsumer<CrashingConsumer>();
            bus.UsingInMemory((context, cfg) =>
            {
                cfg.RespondToAppExceptions();
                cfg.UseInMemoryOutbox(context);
                cfg.UseRetryExceptBusinessErrors(1, 1);
                cfg.ConfigureEndpoints(context);
            });
        });

        _provider = services.BuildServiceProvider();
        await _provider.GetRequiredService<IBusControl>().StartAsync();
        _bus = new TransportBus(_provider.GetRequiredService<IBus>());
    }

    public async Task DisposeAsync()
    {
        await _provider.GetRequiredService<IBusControl>().StopAsync();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task CallerAcceptingFailureContract_GetsFailureWithErrorCode_WithoutRetries()
    {
        var response = await _bus.SendRequestAsync<BusinessErrorRequest, OkResponse, BaseFailedResponseContract>(
            new BusinessErrorRequest(ErrorCodes.TokenExpired));

        Assert.True(response.IsFailure);
        Assert.Equal(ErrorCodes.TokenExpired, response.Failure!.Reason);
        Assert.Equal(1, _attempts.Count);
    }

    [Fact]
    public async Task CallerWithoutFailureContract_GetsAppExceptionWithErrorCode_WithoutRetries()
    {
        var exception = await Assert.ThrowsAsync<AppException>(() =>
            _bus.SendRequestAsync<BusinessErrorRequest, OkResponse>(new BusinessErrorRequest(ErrorCodes.UserNotFound)));

        Assert.Equal(ErrorCodes.UserNotFound, exception.ErrorCode);
        Assert.Equal(1, _attempts.Count);
    }

    [Fact]
    public async Task UnexpectedException_IsStillRetriedAndFaulted()
    {
        await Assert.ThrowsAsync<RequestFaultException>(() =>
            _bus.SendRequestAsync<CrashRequest, OkResponse, BaseFailedResponseContract>(new CrashRequest()));

        Assert.Equal(3, _attempts.Count);
    }

    public sealed class Attempts
    {
        private int _count;

        public int Count => _count;

        public void Add() => Interlocked.Increment(ref _count);
    }

    public sealed record BusinessErrorRequest(string ErrorCode);

    public sealed record CrashRequest;

    public sealed record OkResponse;

    public sealed class BusinessErrorConsumer(Attempts attempts) : IConsumer<BusinessErrorRequest>
    {
        public Task Consume(ConsumeContext<BusinessErrorRequest> context)
        {
            attempts.Add();
            throw new AppException(context.Message.ErrorCode);
        }
    }

    public sealed class CrashingConsumer(Attempts attempts) : IConsumer<CrashRequest>
    {
        public Task Consume(ConsumeContext<CrashRequest> context)
        {
            attempts.Add();
            throw new InvalidOperationException("Database is down");
        }
    }
}
