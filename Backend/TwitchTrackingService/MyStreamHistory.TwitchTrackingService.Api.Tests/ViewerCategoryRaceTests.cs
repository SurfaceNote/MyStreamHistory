using System.Reflection;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using MyStreamHistory.Shared.Api.Features;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.ViewerService.Api.Consumers;
using MyStreamHistory.ViewerService.Application.DTOs;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Application.Services;
using MyStreamHistory.ViewerService.Domain.Entities;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Api.Tests;

public class ViewerCategoryRaceTests
{
    [Fact]
    public async Task CategoryArrivesWhileStreamCreatedLoadsCategory_PreservesCategoryAndBufferedMessages()
    {
        var fixture = new Fixture();
        var lookupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstLookup = new TaskCompletionSource<ActiveStreamCategoryDto?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        fixture.Categories.Lookup = () =>
        {
            if (++calls != 1) return Task.FromResult<ActiveStreamCategoryDto?>(fixture.ActiveStream);
            lookupStarted.SetResult();
            return firstLookup.Task;
        };
        var streamCreated = new StreamCreatedConsumer(new(true), fixture.Tracking, fixture.Categories,
            NullLogger<StreamCreatedConsumer>.Instance);
        var creation = streamCreated.Consume(Context(new StreamCreatedEventContract
        {
            BroadcasterUserId = 44912050, StreamSessionId = fixture.Session
        }));

        try
        {
            await lookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.Consumer.Consume(Context(fixture.Event));
            fixture.Buffer.AddChatMessage("44912050", "viewer", 140);
        }
        finally
        {
            // The initial lookup happened before the category existed, as in the production race.
            firstLookup.TrySetResult(new() { StreamSessionId = fixture.Session, IsLiveConfirmed = true });
            await creation;
        }

        var snapshot = fixture.Buffer.CreateSnapshot().StreamSnapshots["44912050"];
        Assert.Equal(fixture.Category, snapshot.CurrentCategoryId);
        Assert.Equal(140, snapshot.ChatMessages["viewer"]);
        Assert.Equal(1, fixture.Subscriptions.SubscribeCount);
        Assert.Contains(fixture.Event.MessageId, fixture.Processed.Ids);

        await fixture.Consumer.Consume(Context(fixture.Event));
        Assert.Equal(1, fixture.Subscriptions.SubscribeCount);
    }

    [Fact]
    public async Task CategoryArrivesBeforeStreamCreated_UsesEventCategoryWhenActiveSessionHasNoCategoryYet()
    {
        var fixture = new Fixture();
        fixture.ActiveStream.StreamCategoryId = null;

        await fixture.Consumer.Consume(Context(fixture.Event));

        var snapshot = fixture.Buffer.CreateSnapshot().StreamSnapshots["44912050"];
        Assert.Equal(fixture.Session, snapshot.StreamSessionId);
        Assert.Equal(fixture.Category, snapshot.CurrentCategoryId);
        Assert.Equal(1, fixture.Subscriptions.SubscribeCount);
    }

    [Fact]
    public async Task OlderCategoryEventBeforeBufferExists_UsesLatestAuthoritativeCategory()
    {
        var fixture = new Fixture();
        var latestCategory = Guid.NewGuid();
        fixture.ActiveStream.StreamCategoryId = latestCategory;

        await fixture.Consumer.Consume(Context(fixture.Event));

        Assert.Equal(latestCategory, fixture.Buffer.CreateSnapshot().StreamSnapshots["44912050"].CurrentCategoryId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EventForOldSession_DoesNotInitializeOrReplaceCurrentStream(bool hasBuffer)
    {
        var fixture = new Fixture();
        var currentSession = Guid.NewGuid();
        var currentCategory = Guid.NewGuid();
        fixture.ActiveStream.StreamSessionId = currentSession;
        fixture.ActiveStream.StreamCategoryId = currentCategory;
        if (hasBuffer) fixture.Buffer.InitializeStream("44912050", currentSession, currentCategory);

        await fixture.Consumer.Consume(Context(fixture.Event));

        Assert.False(fixture.Buffer.IsStreamActive("44912050", fixture.Session));
        Assert.Equal(0, fixture.Subscriptions.SubscribeCount);
        if (hasBuffer)
            Assert.Equal(currentCategory, fixture.Buffer.CreateSnapshot().StreamSnapshots["44912050"].CurrentCategoryId);
        else
            Assert.Empty(fixture.Buffer.CreateSnapshot().StreamSnapshots);
    }

    [Fact]
    public async Task UnconfirmedStream_DoesNotInitializeAccrualFromCategoryEvent()
    {
        var fixture = new Fixture();
        fixture.ActiveStream.IsLiveConfirmed = false;

        await fixture.Consumer.Consume(Context(fixture.Event));

        Assert.Empty(fixture.Buffer.CreateSnapshot().StreamSnapshots);
        Assert.Equal(0, fixture.Subscriptions.SubscribeCount);
    }

    private sealed class Fixture
    {
        public Guid Session { get; } = Guid.NewGuid();
        public Guid Category { get; } = Guid.NewGuid();
        public ChatMessageBufferService Buffer { get; } = new();
        public FakeCategoryService Categories { get; } = new();
        public FakeSubscriptions Subscriptions { get; } = new();
        public FakeProcessedMessages Processed { get; } = new();
        public ActiveStreamCategoryDto ActiveStream { get; }
        public StreamCategoryChangedEventContract Event { get; }
        public ViewerTrackingService Tracking { get; }
        public StreamCategoryChangedConsumer Consumer { get; }

        public Fixture()
        {
            ActiveStream = new() { StreamSessionId = Session, StreamCategoryId = Category, IsLiveConfirmed = true };
            Categories.Lookup = () => Task.FromResult<ActiveStreamCategoryDto?>(ActiveStream);
            Tracking = new(Buffer, Subscriptions, new FakeTokens(), new StreamLifecycleLock(),
                NullLogger<ViewerTrackingService>.Instance);
            Consumer = new(new(true), Buffer, Processed, NullLogger<StreamCategoryChangedConsumer>.Instance,
                Categories, Tracking);
            Event = new()
            {
                MessageId = Guid.NewGuid().ToString(), BroadcasterUserId = 44912050,
                StreamSessionId = Session, NewCategoryId = Category, CategoryName = "Retro"
            };
        }
    }

    private sealed class FakeCategoryService : IStreamCategoryService
    {
        public Func<Task<ActiveStreamCategoryDto?>> Lookup { get; set; } = () => Task.FromResult<ActiveStreamCategoryDto?>(null);
        public Task<ActiveStreamCategoryDto?> GetActiveStreamCategoryAsync(string twitchUserId, CancellationToken cancellationToken = default)
            => Lookup();
    }

    private sealed class FakeSubscriptions : ITwitchEventSubClient
    {
        public int SubscribeCount { get; private set; }
        public Task<string> SubscribeToChatMessagesAsync(string broadcasterId, string accessToken, CancellationToken cancellationToken = default)
        {
            SubscribeCount++;
            return Task.FromResult("subscription");
        }
        public Task<List<EventSubSubscription>> GetSubscriptionsAsync(string? type = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<EventSubSubscription>());
        public Task UnsubscribeAsync(string subscriptionId, string accessToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<int> CleanupAllChatSubscriptionsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeTokens : IAuthTokenService
    {
        public Task<(string AccessToken, DateTime ExpiresAt)?> GetTwitchAccessTokenAsync(string twitchUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<(string, DateTime)?>(("token", DateTime.UtcNow.AddHours(1)));
        public Task<(string AccessToken, DateTime ExpiresAt)?> RefreshTwitchAccessTokenAsync(string twitchUserId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeProcessedMessages : IProcessedEventSubMessageRepository
    {
        public HashSet<string> Ids { get; } = [];
        public Task<bool> ExistsAsync(string messageId, CancellationToken cancellationToken = default) => Task.FromResult(Ids.Contains(messageId));
        public Task AddAsync(ProcessedEventSubMessage message, CancellationToken cancellationToken = default)
        {
            Ids.Add(message.MessageId);
            return Task.CompletedTask;
        }
        public Task DeleteOlderThanAsync(DateTime threshold, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var context = DispatchProxy.Create<ConsumeContext<T>, MessageContext<T>>();
        ((MessageContext<T>)(object)context).Message = message;
        return context;
    }

    public class MessageContext<T> : DispatchProxy where T : class
    {
        public T Message { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            "get_Message" => Message,
            "get_CancellationToken" => CancellationToken.None,
            _ => throw new NotSupportedException(targetMethod?.Name)
        };
    }
}
