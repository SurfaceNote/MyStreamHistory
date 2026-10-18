using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MyStreamHistory.Shared.Base.Contracts.Community;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.ViewerService.Api.BackgroundServices;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Domain.Entities;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Api.Tests;

public class EventSubHistoryCleanupTests
{
    [Fact]
    public async Task Startup_CleansBothJournalsWithoutWaitingForTimer()
    {
        var processed = new ProcessedMessages();
        var community = new Community();
        using var services = new ServiceCollection()
            .AddSingleton<IProcessedEventSubMessageRepository>(processed)
            .AddSingleton<ICommunityService>(community)
            .BuildServiceProvider();
        using var worker = new EventSubHistoryCleanupBackgroundService(services,
            NullLogger<EventSubHistoryCleanupBackgroundService>.Instance);
        var beforeStart = DateTime.UtcNow.AddHours(-2);

        await worker.StartAsync(default);
        try
        {
            await community.Cleaned.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.InRange(processed.Threshold, beforeStart, DateTime.UtcNow.AddHours(-2));
        }
        finally
        {
            await worker.StopAsync(default);
        }
    }

    private sealed class ProcessedMessages : IProcessedEventSubMessageRepository
    {
        public DateTime Threshold { get; private set; }
        public Task DeleteOlderThanAsync(DateTime threshold, CancellationToken cancellationToken = default)
        {
            Threshold = threshold;
            return Task.CompletedTask;
        }
        public Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(ProcessedEventSubMessage message, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Community : ICommunityService
    {
        public TaskCompletionSource Cleaned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<int> DeleteExpiredMessageReceiptsAsync(CancellationToken ct)
        {
            Cleaned.SetResult();
            return Task.FromResult(3);
        }
        public Task RecordPollAsync(string channel, Guid session, DateTime observedAt, IReadOnlyCollection<TwitchChatterDto>? chatters, CancellationToken ct) => throw new NotSupportedException();
        public Task RecordMessageAsync(ChatMessageEventContract message, Guid session, CancellationToken ct) => throw new NotSupportedException();
        public Task<CommunityResponse> HandleAsync(CommunityRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task RecalculatePendingAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
