using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyStreamHistory.Gateway.Application.Commands.TwitchEventSub;
using MyStreamHistory.Gateway.Application.Options;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.Shared.Base.Error;
using MyStreamHistory.Shared.Base.Exceptions;
using Xunit;

namespace MyStreamHistory.Gateway.Application.Tests;

public class ProcessEventSubCommandHandlerTests
{
    private const string Secret = "test-eventsub-secret";
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    // Twitch sends nanosecond precision; parsing must accept more than 7 fractional digits.
    private static readonly string FreshTimestamp = "2026-11-18T11:59:30.123456789Z";

    private const string StreamOnlineBody = """
        {"subscription":{"id":"sub-1","type":"stream.online","version":"1","status":"enabled"},
         "event":{"id":"9001","broadcaster_user_id":"1337","broadcaster_user_login":"cool_user","broadcaster_user_name":"Cool_User",
                  "type":"live","started_at":"2026-11-18T11:58:00.5Z"}}
        """;

    [Fact]
    public async Task ValidChallenge_ReturnsChallenge()
    {
        var (handler, bus) = CreateHandler();

        var result = await handler.Handle(Signed("""{"challenge":"pogchamp-kappa-360noscope","subscription":{}}""", "webhook_callback_verification"), default);

        Assert.Equal("pogchamp-kappa-360noscope", result);
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task StreamOnline_PublishesContractWithUtcStart()
    {
        var (handler, bus) = CreateHandler();

        var result = await handler.Handle(Signed(StreamOnlineBody), default);

        Assert.Null(result);
        var contract = Assert.IsType<StreamOnlineEventContract>(Assert.Single(bus.Published));
        Assert.Equal(1337, contract.BroadcasterUserId);
        Assert.Equal("cool_user", contract.BroadcasterUserLogin);
        Assert.Equal(DateTimeKind.Utc, contract.StartedAt.Kind);
        Assert.Equal(new DateTime(2026, 11, 18, 11, 58, 0, 500, DateTimeKind.Utc), contract.StartedAt);
    }

    [Fact]
    public async Task StreamOffline_UsesMessageTimestampAsUtc()
    {
        var (handler, bus) = CreateHandler();
        const string body = """
            {"subscription":{"type":"stream.offline"},
             "event":{"broadcaster_user_id":"1337","broadcaster_user_login":"cool_user","broadcaster_user_name":"Cool_User"}}
            """;

        await handler.Handle(Signed(body, timestamp: "2026-11-18T12:59:30+01:00"), default);

        var contract = Assert.IsType<StreamOfflineEventContract>(Assert.Single(bus.Published));
        Assert.Equal(new DateTime(2026, 11, 18, 11, 59, 30, DateTimeKind.Utc), contract.OccurredAt);
        Assert.Equal(DateTimeKind.Utc, contract.OccurredAt!.Value.Kind);
    }

    [Fact]
    public async Task ChannelUpdate_ToleratesEmptyCategory()
    {
        var (handler, bus) = CreateHandler();
        const string body = """
            {"subscription":{"type":"channel.update"},
             "event":{"broadcaster_user_id":"1337","broadcaster_user_login":"cool_user","broadcaster_user_name":"Cool_User",
                      "title":"","language":"en","category_id":"","category_name":null}}
            """;

        await handler.Handle(Signed(body), default);

        var contract = Assert.IsType<ChannelUpdateEventContract>(Assert.Single(bus.Published));
        Assert.Equal(string.Empty, contract.CategoryId);
        Assert.Equal(string.Empty, contract.CategoryName);
    }

    [Fact]
    public async Task InvalidSignature_IsRejected()
    {
        var (handler, bus) = CreateHandler();
        var request = Signed(StreamOnlineBody) with { MessageSignature = "sha256=" + new string('0', 64) };

        var ex = await Assert.ThrowsAsync<AppException>(() => handler.Handle(request, default));

        Assert.Equal(ErrorCodes.PermissionDenied, ex.ErrorCode);
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task TamperedBody_IsRejected()
    {
        var (handler, bus) = CreateHandler();
        var request = Signed(StreamOnlineBody) with { RequestBody = StreamOnlineBody.Replace("1337", "1338") };

        var ex = await Assert.ThrowsAsync<AppException>(() => handler.Handle(request, default));

        Assert.Equal(ErrorCodes.PermissionDenied, ex.ErrorCode);
        Assert.Empty(bus.Published);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sha256=")]
    [InlineData("sha256=not-hex")]
    [InlineData("md5=abcdef")]
    public void MalformedSignatureHeader_IsInvalid(string header) =>
        Assert.False(ProcessEventSubCommandHandler.IsSignatureValid(Secret, "id", FreshTimestamp, "{}", header));

    [Fact]
    public void SignatureCheck_AcceptsUppercaseHex()
    {
        var signature = Sign("id", FreshTimestamp, "{}");
        var upper = ProcessEventSubCommandHandler.SignaturePrefix + signature[ProcessEventSubCommandHandler.SignaturePrefix.Length..].ToUpperInvariant();

        Assert.True(ProcessEventSubCommandHandler.IsSignatureValid(Secret, "id", FreshTimestamp, "{}", upper));
    }

    [Theory]
    [InlineData("2026-11-18T11:49:59Z")]      // just over 10 minutes old
    [InlineData("2026-11-18T10:30:00Z")]      // far in the past
    [InlineData("2026-11-18T12:01:01Z")]      // beyond allowed clock skew
    [InlineData("not-a-timestamp")]
    public async Task UnacceptableTimestamp_IsRejectedEvenWhenSigned(string timestamp)
    {
        var (handler, bus) = CreateHandler();

        var ex = await Assert.ThrowsAsync<AppException>(() => handler.Handle(Signed(StreamOnlineBody, timestamp: timestamp), default));

        Assert.Equal(ErrorCodes.PermissionDenied, ex.ErrorCode);
        Assert.Empty(bus.Published);
    }

    [Theory]
    [InlineData("2026-11-18T11:50:00Z")]      // exactly the max age
    [InlineData("2026-11-18T12:00:30Z")]      // slightly ahead, within skew
    [InlineData("2026-11-18T12:55:00+01:00")] // offset form of a fresh UTC instant
    public async Task AcceptableTimestamp_IsProcessed(string timestamp)
    {
        var (handler, bus) = CreateHandler();

        await handler.Handle(Signed(StreamOnlineBody, timestamp: timestamp), default);

        Assert.Single(bus.Published);
    }

    [Theory]
    [InlineData("""{"subscription":{"type":"stream.online"},"event":{"broadcaster_user_login":"x","broadcaster_user_name":"x","type":"live","started_at":"2026-11-18T11:58:00Z"}}""")]
    [InlineData("""{"subscription":{"type":"stream.online"},"event":{"broadcaster_user_id":"abc","broadcaster_user_login":"x","broadcaster_user_name":"x","type":"live","started_at":"2026-11-18T11:58:00Z"}}""")]
    [InlineData("""{"subscription":{"type":"stream.online"},"event":{"broadcaster_user_id":"1337","broadcaster_user_login":null,"broadcaster_user_name":"x","type":"live","started_at":"2026-11-18T11:58:00Z"}}""")]
    [InlineData("""{"subscription":{"type":"stream.online"},"event":{"broadcaster_user_id":"1337","broadcaster_user_login":"x","broadcaster_user_name":"x","type":"live","started_at":"yesterday"}}""")]
    [InlineData("""{"subscription":{"type":"channel.chat.message"},"event":{"broadcaster_user_id":"1337","broadcaster_user_login":"x","broadcaster_user_name":"x"}}""")]
    [InlineData("""{"subscription":{"type":"stream.online"}}""")]
    [InlineData("""{"event":{}}""")]
    [InlineData("""not json at all""")]
    public async Task SignedButMalformedNotification_IsAcknowledgedWithoutPublishing(string body)
    {
        var (handler, bus) = CreateHandler();

        var result = await handler.Handle(Signed(body), default);

        Assert.Null(result);
        Assert.Empty(bus.Published);
    }

    [Fact]
    public async Task ChallengeWithoutChallengeField_ReturnsNull()
    {
        var (handler, _) = CreateHandler();

        var result = await handler.Handle(Signed("""{"subscription":{}}""", "webhook_callback_verification"), default);

        Assert.Null(result);
    }

    [Fact]
    public async Task Revocation_IsAcknowledgedAndLogged()
    {
        var logger = new ListLogger();
        var (handler, bus) = CreateHandler(logger);
        const string body = """
            {"subscription":{"id":"sub-1","status":"authorization_revoked","type":"stream.online","version":"1",
                             "condition":{"broadcaster_user_id":"1337"}}}
            """;

        var result = await handler.Handle(Signed(body, "revocation"), default);

        Assert.Null(result);
        Assert.Empty(bus.Published);
        var warning = Assert.Single(logger.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
        Assert.Contains("authorization_revoked", warning.Message);
        Assert.Contains("1337", warning.Message);
    }

    [Fact]
    public async Task PublishFailure_Propagates()
    {
        var (handler, bus) = CreateHandler();
        bus.Fail = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(Signed(StreamOnlineBody), default));
    }

    private static (ProcessEventSubCommandHandler Handler, RecordingBus Bus) CreateHandler(ListLogger? logger = null)
    {
        var bus = new RecordingBus();
        var handler = new ProcessEventSubCommandHandler(
            bus,
            Microsoft.Extensions.Options.Options.Create(new TwitchEventSubOptions { Secret = Secret, MaxAgeMinutes = 10 }),
            new FixedTimeProvider(Now),
            (Microsoft.Extensions.Logging.ILogger<ProcessEventSubCommandHandler>?)logger ?? NullLogger<ProcessEventSubCommandHandler>.Instance);
        return (handler, bus);
    }

    private static ProcessEventSubCommand Signed(string body, string messageType = "notification", string? timestamp = null)
    {
        const string messageId = "befa7b53-d79d-478f-86b9-120f112b044e";
        timestamp ??= FreshTimestamp;
        return new ProcessEventSubCommand(body, messageId, timestamp, Sign(messageId, timestamp, body), messageType);
    }

    private static string Sign(string messageId, string timestamp, string body)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(messageId + timestamp + body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingBus : ITransportBus
    {
        public List<object> Published { get; } = [];
        public bool Fail { get; set; }

        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            if (Fail)
            {
                throw new InvalidOperationException("bus down");
            }

            Published.Add(message);
            return Task.CompletedTask;
        }

        public Task<EmptyResponse> SendRequestAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : class => throw new NotSupportedException();

        public Task<TransportResponse<TResponse>> SendRequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : class where TResponse : class => throw new NotSupportedException();

        public Task<TransportResponse<TSuccessResponse, TFailureResponse>> SendRequestAsync<TRequest, TSuccessResponse, TFailureResponse>(
            TRequest request, CancellationToken cancellationToken = default)
            where TRequest : class where TSuccessResponse : class where TFailureResponse : class => throw new NotSupportedException();
    }

    private sealed class ListLogger : Microsoft.Extensions.Logging.ILogger<ProcessEventSubCommandHandler>
    {
        public List<(Microsoft.Extensions.Logging.LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
