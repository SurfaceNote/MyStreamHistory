using System.Threading.Channels;
using MyStreamHistory.ViewerService.Application.DTOs;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Api.BackgroundServices;

public class ViewerDataProcessingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ViewerDataProcessingBackgroundService> _logger;
    private readonly Channel<DataCollectionSnapshot> _processingQueue;
    private DateTime _lastBootstrapAttempt = DateTime.MinValue;
    private static readonly TimeSpan BootstrapRetryInterval = TimeSpan.FromMinutes(5);

    public ViewerDataProcessingBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ViewerDataProcessingBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _processingQueue = Channel.CreateUnbounded<DataCollectionSnapshot>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ViewerDataProcessingBackgroundService started");

        await BootstrapActiveStreamsAsync(stoppingToken);

        // Start both tasks in parallel
        var collectionTask = DataCollectionLoopAsync(stoppingToken);
        var processingTask = DataProcessingLoopAsync(stoppingToken);

        await Task.WhenAll(collectionTask, processingTask);
    }

    private async Task DataCollectionLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (ShouldRetryBootstrap())
                {
                    await BootstrapActiveStreamsAsync(stoppingToken);
                }

                var snapshot = CreateDataSnapshot();
                await _processingQueue.Writer.WriteAsync(snapshot, stoppingToken);
                
                _logger.LogInformation("Data snapshot created at {Timestamp} with {Count} streams",
                    snapshot.Timestamp, snapshot.StreamSnapshots.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating data snapshot");
            }
        }
    }

    private async Task DataProcessingLoopAsync(CancellationToken stoppingToken)
    {
        await foreach (var snapshot in _processingQueue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessSnapshotAsync(snapshot, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing snapshot at {Timestamp}", snapshot.Timestamp);
            }
        }
    }

    private DataCollectionSnapshot CreateDataSnapshot()
    {
        using var scope = _serviceProvider.CreateScope();
        var bufferService = scope.ServiceProvider.GetRequiredService<IChatMessageBufferService>();
        
        return bufferService.CreateSnapshot();
    }

    private async Task ProcessSnapshotAsync(DataCollectionSnapshot snapshot, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var processingService = scope.ServiceProvider.GetRequiredService<IViewerDataProcessingService>();
        
        var startTime = DateTime.UtcNow;
        await processingService.ProcessSnapshotAsync(snapshot, cancellationToken);
        var duration = DateTime.UtcNow - startTime;
        
        _logger.LogInformation("Processed snapshot in {Duration}ms", duration.TotalMilliseconds);
    }

    private async Task BootstrapActiveStreamsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _lastBootstrapAttempt = DateTime.UtcNow;

            using var scope = _serviceProvider.CreateScope();
            var bootstrapService = scope.ServiceProvider.GetRequiredService<IActiveStreamBootstrapService>();
            await bootstrapService.BootstrapActiveStreamsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bootstrapping active viewer streams");
        }
    }

    private bool ShouldRetryBootstrap()
    {
        if (DateTime.UtcNow - _lastBootstrapAttempt < BootstrapRetryInterval)
        {
            return false;
        }

        using var scope = _serviceProvider.CreateScope();
        var bufferService = scope.ServiceProvider.GetRequiredService<IChatMessageBufferService>();

        return bufferService.GetActiveStreamCount() == 0;
    }
}

