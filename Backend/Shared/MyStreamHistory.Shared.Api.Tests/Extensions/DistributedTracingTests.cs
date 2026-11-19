using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Infrastructure.Transport;
using Sentry.AspNetCore;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;
using Xunit;

namespace MyStreamHistory.Shared.Api.Tests.Extensions;

public class DistributedTracingTests
{
    private static readonly ActivitySource Npgsql = new("Npgsql");
    private const string ConsumerQuery = "SELECT pong";

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public async Task BusConsumer_ContinuesTraceOfHttpRequest_WithSameSamplingDecision(string sampleRate, bool recorded)
    {
        await using var app = await StartAppAsync(sampleRate);

        var response = await app.GetTestClient().GetStringAsync("/ping");
        var parts = response.Split('|');

        // Gateway → RabbitMQ → consumer is one trace, so logs of both sides share the TraceId
        // even when the trace is not sampled for Sentry.
        Assert.Equal(parts[0], parts[2]);
        Assert.Equal(recorded.ToString(), parts[1]);
        Assert.Equal(recorded.ToString(), parts[3]);
    }

    [Fact]
    public async Task DatabaseSpan_IsRecordedInsideRequest_ButDoesNotStartTraceOnItsOwn()
    {
        await using var app = await StartAppAsync("1");

        var insideRequest = await app.GetTestClient().GetStringAsync("/query");
        using var outsideRequest = Npgsql.StartActivity("SELECT", ActivityKind.Client);

        Assert.Equal(bool.TrueString, insideRequest);
        Assert.NotNull(outsideRequest);
        Assert.False(outsideRequest.Recorded);
    }

    [Fact]
    public async Task SampledTrace_ReachesSentry_WithQueryNestedInBusConsumer()
    {
        var transport = new RecordingTransport();
        await using var app = await StartAppAsync("1", transport);

        // Request → bus send → consume → database query. Sentry used to turn the request into a no-op
        // transaction and throw on the third nested span, so the consumer failed on every sampled message.
        // Before the fix the failing consumer left the request hanging; fail the test instead of the run.
        await app.GetTestClient().GetStringAsync("/ping").WaitAsync(TimeSpan.FromSeconds(30));

        var transaction = await transport.WaitForTransactionAsync(ConsumerQuery, TimeSpan.FromSeconds(10));
        Assert.NotNull(transaction);
    }

    private static async Task<WebApplication> StartAppAsync(string sampleRate, ITransport? sentryTransport = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        // An empty DSN disables the SDK and hides how Sentry handles spans; a test transport keeps it offline.
        builder.Configuration["Sentry:Dsn"] = sentryTransport is null ? string.Empty : "https://public@sentry.invalid/1";
        builder.Configuration["Sentry:TracesSampleRate"] = sampleRate;
        builder.AddSentryObservability();
        if (sentryTransport is not null)
        {
            builder.Services.Configure<SentryAspNetCoreOptions>(options => options.Transport = sentryTransport);
        }
        builder.Services.AddMassTransit(bus =>
        {
            bus.AddConsumer<PingConsumer>();
            bus.UsingInMemory((context, cfg) =>
            {
                cfg.PropagateTraceContext();
                cfg.ConfigureEndpoints(context);
            });
        });

        var app = builder.Build();
        app.MapGet("/ping", async (IRequestClient<Ping> client) =>
        {
            var pong = await client.GetResponse<Pong>(new Ping());
            var request = Activity.Current!;
            return $"{request.TraceId}|{request.Recorded}|{pong.Message.TraceId}|{pong.Message.Recorded}";
        });
        app.MapGet("/query", () =>
        {
            using var query = Npgsql.StartActivity("SELECT", ActivityKind.Client);
            return query?.Recorded.ToString() ?? "no activity";
        });

        await app.StartAsync();
        return app;
    }

    public sealed record Ping;

    public sealed record Pong(string TraceId, bool Recorded);

    public sealed class PingConsumer : IConsumer<Ping>
    {
        public Task Consume(ConsumeContext<Ping> context)
        {
            var activity = Activity.Current!;
            using (Npgsql.StartActivity(ConsumerQuery, ActivityKind.Client))
            {
            }

            return context.RespondAsync(new Pong(activity.TraceId.ToString(), activity.Recorded));
        }
    }

    private sealed class RecordingTransport : ITransport
    {
        private readonly ConcurrentQueue<string> _envelopes = new();
        private readonly SemaphoreSlim _received = new(0);

        public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();
            await envelope.SerializeAsync(stream, null, cancellationToken);
            _envelopes.Enqueue(Encoding.UTF8.GetString(stream.ToArray()));
            _received.Release();
        }

        public async Task<string?> WaitForTransactionAsync(string spanDescription, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            do
            {
                var match = _envelopes.FirstOrDefault(envelope =>
                    envelope.Contains("\"type\":\"transaction\"") && envelope.Contains(spanDescription));
                if (match is not null)
                {
                    return match;
                }
            }
            while (await _received.WaitAsync(deadline - DateTime.UtcNow > TimeSpan.Zero ? deadline - DateTime.UtcNow : TimeSpan.Zero));

            return null;
        }
    }
}
