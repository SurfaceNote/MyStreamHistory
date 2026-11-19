using System.Diagnostics;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Infrastructure.Transport;
using Xunit;

namespace MyStreamHistory.Shared.Api.Tests.Extensions;

public class DistributedTracingTests
{
    private static readonly ActivitySource Npgsql = new("Npgsql");

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

    private static async Task<WebApplication> StartAppAsync(string sampleRate)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["Sentry:Dsn"] = string.Empty;
        builder.Configuration["Sentry:TracesSampleRate"] = sampleRate;
        builder.AddSentryObservability();
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
            return context.RespondAsync(new Pong(activity.TraceId.ToString(), activity.Recorded));
        }
    }
}
