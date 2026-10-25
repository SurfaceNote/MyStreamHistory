using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MyStreamHistory.AuthService.Api.BackgroundServices;
using MyStreamHistory.AuthService.Api.Extensions;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Infrastructure.Options;
using MyStreamHistory.Shared.Api.Features;
using Xunit;

namespace MyStreamHistory.AuthService.Tests;

public class TwitchProfileSyncBackgroundServiceTests
{
    [Fact]
    public async Task StartsAfterHostStartup_AndRunsAtConfiguredInterval()
    {
        var clock = new FakeTimeProvider();
        var sync = new Sync();
        using var host = CreateHost(sync, clock, 10);
        Assert.Equal(0, sync.Count);

        await host.StartAsync();
        Assert.Equal(1, await sync.Next());
        clock.Advance(TimeSpan.FromMinutes(9));
        Assert.Equal(1, sync.Count);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(2, await sync.Next());

        await host.StopAsync();
        clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(2, sync.Count);
    }

    [Fact]
    public async Task SlowCyclesNeverOverlap_AndMissedTicksAreCoalesced()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sync = new Sync { Run = count => count == 1 ? release.Task : Task.CompletedTask };
        var clock = new FakeTimeProvider();
        using var host = CreateHost(sync, clock);
        await host.StartAsync();
        Assert.Equal(1, await sync.Next());

        clock.Advance(TimeSpan.FromHours(3));
        Assert.Equal(1, sync.Count);
        release.SetResult();
        Assert.Equal(2, await sync.Next());
        await host.StopAsync();
        Assert.Equal(2, sync.Count);
    }

    [Fact]
    public async Task FailedCycleRetriesOnNextInterval()
    {
        var clock = new FakeTimeProvider();
        var sync = new Sync { Run = count => count == 1 ? Task.FromException(new HttpRequestException("Unavailable")) : Task.CompletedTask };
        using var host = CreateHost(sync, clock);
        await host.StartAsync();
        Assert.Equal(1, await sync.Next());
        clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(2, await sync.Next());
        await host.StopAsync();
    }

    [Fact]
    public async Task DisabledWorkerDoesNotResolveAnyDependencies()
    {
        using var worker = new TwitchProfileSyncBackgroundService(null!, null!, null!, null!,
            new TwitchDataCollectionFeature(false), NullLogger<TwitchProfileSyncBackgroundService>.Instance);
        await worker.StartAsync(CancellationToken.None);
        await worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegistrationRespectsFeatureFlag_AndDefaultsToSixtyMinutes(bool enabled)
    {
        var services = new ServiceCollection();
        services.AddTwitchProfileSync(new ConfigurationBuilder().Build(), new TwitchDataCollectionFeature(enabled));
        Assert.Equal(enabled, services.Any(d => d.ServiceType == typeof(IHostedService)
            && d.ImplementationType == typeof(TwitchProfileSyncBackgroundService)));
        using var provider = services.BuildServiceProvider();
        Assert.Equal(60, provider.GetRequiredService<IOptions<TwitchProfileSyncOptions>>().Value.IntervalMinutes);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1441")]
    public void InvalidIntervalIsRejected(string interval)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TwitchProfileSync:IntervalMinutes"] = interval
        }).Build();
        var services = new ServiceCollection();
        services.AddTwitchProfileSync(configuration, new TwitchDataCollectionFeature(true));
        using var provider = services.BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<TwitchProfileSyncOptions>>().Value);
    }

    private static IHost CreateHost(Sync sync, FakeTimeProvider clock, int intervalMinutes = 60)
        => new HostBuilder().ConfigureServices(services =>
        {
            services.AddLogging();
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton(new TwitchDataCollectionFeature(true));
            services.AddSingleton<ITwitchProfileSyncService>(sync);
            services.Configure<TwitchProfileSyncOptions>(options => { });
            services.AddSingleton<IOptions<TwitchProfileSyncOptions>>(Options.Create(new TwitchProfileSyncOptions { IntervalMinutes = intervalMinutes }));
            services.AddHostedService<TwitchProfileSyncBackgroundService>();
        }).Build();

    private sealed class Sync : ITwitchProfileSyncService
    {
        private int _count;
        private readonly Channel<int> _started = Channel.CreateUnbounded<int>();
        public int Count => Volatile.Read(ref _count);
        public Func<int, Task> Run { get; init; } = _ => Task.CompletedTask;

        public async Task SyncAsync(CancellationToken cancellationToken = default)
        {
            var count = Interlocked.Increment(ref _count);
            _started.Writer.TryWrite(count);
            await Run(count).WaitAsync(cancellationToken);
        }

        public Task<int> Next() => _started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
