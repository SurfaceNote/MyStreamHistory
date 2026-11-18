using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyStreamHistory.Gateway.Application.Options;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.Shared.Base.Error;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Commands.TwitchEventSub;

/// <summary>
/// Verifies and dispatches one Twitch EventSub webhook delivery.
/// Throws <see cref="AppException"/> with <see cref="ErrorCodes.PermissionDenied"/> when the request
/// cannot be proven to come from Twitch (bad signature, stale or malformed timestamp).
/// A correctly signed message whose payload cannot be parsed is logged and acknowledged instead of failing:
/// Twitch retries every non-2xx answer and revokes the subscription after repeated failures, and retrying
/// a payload we cannot read would never succeed.
/// </summary>
public class ProcessEventSubCommandHandler : IRequestHandler<ProcessEventSubCommand, string?>
{
    public const string SignaturePrefix = "sha256=";

    /// <summary>Tolerated clock difference for timestamps slightly ahead of our clock.</summary>
    public static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(1);

    private readonly ITransportBus _transportBus;
    private readonly TwitchEventSubOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProcessEventSubCommandHandler> _logger;

    public ProcessEventSubCommandHandler(
        ITransportBus transportBus,
        IOptions<TwitchEventSubOptions> options,
        TimeProvider timeProvider,
        ILogger<ProcessEventSubCommandHandler> logger)
    {
        _transportBus = transportBus;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<string?> Handle(ProcessEventSubCommand request, CancellationToken cancellationToken)
    {
        if (!IsSignatureValid(_options.Secret, request.MessageId, request.MessageTimestamp, request.RequestBody, request.MessageSignature))
        {
            _logger.LogWarning("Rejected Twitch EventSub message {MessageId}: invalid signature", request.MessageId);
            throw new AppException(ErrorCodes.PermissionDenied, "Invalid signature");
        }

        if (!TryParseTimestamp(request.MessageTimestamp, out var sentAt) ||
            !IsTimestampFresh(sentAt, _timeProvider.GetUtcNow(), TimeSpan.FromMinutes(_options.MaxAgeMinutes)))
        {
            _logger.LogWarning(
                "Rejected Twitch EventSub message {MessageId}: timestamp {MessageTimestamp} is missing, stale or in the future",
                request.MessageId, request.MessageTimestamp);
            throw new AppException(ErrorCodes.PermissionDenied, "Message timestamp is not acceptable");
        }

        try
        {
            using var document = JsonDocument.Parse(request.RequestBody);
            var root = document.RootElement;

            switch (request.MessageType)
            {
                case "webhook_callback_verification":
                    _logger.LogInformation("Responding to Twitch EventSub challenge");
                    return EventSubJson.RequireString(root, "challenge");

                case "notification":
                    await HandleNotificationAsync(root, request, sentAt, cancellationToken);
                    return null;

                case "revocation":
                    LogRevocation(root, request.MessageId);
                    return null;

                default:
                    _logger.LogWarning("Ignoring unknown Twitch EventSub message type {MessageType}", request.MessageType);
                    return null;
            }
        }
        catch (Exception ex) when (ex is JsonException or EventSubPayloadException)
        {
            _logger.LogError(
                ex,
                "Dropping signed Twitch EventSub message {MessageId} of type {MessageType}: payload could not be parsed",
                request.MessageId, request.MessageType);
            return null;
        }
    }

    private async Task HandleNotificationAsync(
        JsonElement root,
        ProcessEventSubCommand request,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        var subscription = EventSubJson.RequireObject(root, "subscription");
        var eventData = EventSubJson.RequireObject(root, "event");
        var subscriptionType = EventSubJson.RequireString(subscription, "type");

        switch (subscriptionType)
        {
            case "stream.online":
                await PublishAsync(BuildStreamOnline(eventData, request.MessageId), cancellationToken);
                break;
            case "stream.offline":
                await PublishAsync(BuildStreamOffline(eventData, request.MessageId, sentAt), cancellationToken);
                break;
            case "channel.chat.message":
                await PublishAsync(BuildChatMessage(eventData, request.MessageId), cancellationToken);
                break;
            case "channel.update":
                await PublishAsync(BuildChannelUpdate(eventData, request.MessageId), cancellationToken);
                break;
            default:
                _logger.LogWarning("Unknown EventSub subscription type: {Type}", subscriptionType);
                break;
        }
    }

    private Task PublishAsync<T>(T contract, CancellationToken cancellationToken) where T : class
    {
        _logger.LogDebug("Publishing {ContractType} from Twitch EventSub", typeof(T).Name);
        return _transportBus.PublishAsync(contract, cancellationToken);
    }

    private void LogRevocation(JsonElement root, string messageId)
    {
        var subscription = EventSubJson.RequireObject(root, "subscription");
        string? broadcasterId = null;
        if (subscription.TryGetProperty("condition", out var condition) && condition.ValueKind == JsonValueKind.Object)
        {
            broadcasterId = EventSubJson.OptionalString(condition, "broadcaster_user_id");
        }

        // Twitch has already removed the subscription; SubscriptionSyncService recreates stream subscriptions
        // on its next run if the broadcaster still has a valid authorization.
        _logger.LogWarning(
            "Twitch revoked EventSub subscription {SubscriptionId} ({SubscriptionType}) for broadcaster {BroadcasterUserId}: {Status}. Message {MessageId}",
            EventSubJson.OptionalString(subscription, "id"),
            EventSubJson.OptionalString(subscription, "type"),
            broadcasterId,
            EventSubJson.OptionalString(subscription, "status"),
            messageId);
    }

    private static StreamOnlineEventContract BuildStreamOnline(JsonElement eventData, string messageId) => new()
    {
        MessageId = messageId,
        BroadcasterUserId = EventSubJson.RequireNumericId(eventData, "broadcaster_user_id"),
        BroadcasterUserLogin = EventSubJson.RequireString(eventData, "broadcaster_user_login"),
        BroadcasterUserName = EventSubJson.RequireString(eventData, "broadcaster_user_name"),
        StartedAt = EventSubJson.RequireUtcDateTime(eventData, "started_at"),
        Type = EventSubJson.RequireString(eventData, "type")
    };

    private static StreamOfflineEventContract BuildStreamOffline(JsonElement eventData, string messageId, DateTimeOffset sentAt) => new()
    {
        MessageId = messageId,
        BroadcasterUserId = EventSubJson.RequireNumericId(eventData, "broadcaster_user_id"),
        BroadcasterUserLogin = EventSubJson.RequireString(eventData, "broadcaster_user_login"),
        BroadcasterUserName = EventSubJson.RequireString(eventData, "broadcaster_user_name"),
        OccurredAt = sentAt.UtcDateTime
    };

    private static ChatMessageEventContract BuildChatMessage(JsonElement eventData, string messageId)
    {
        var message = EventSubJson.RequireObject(eventData, "message");
        var messageText = EventSubJson.OptionalString(message, "text") ?? string.Empty;

        return new ChatMessageEventContract
        {
            MessageId = messageId,
            BroadcasterUserId = EventSubJson.RequireString(eventData, "broadcaster_user_id"),
            TwitchMessageId = EventSubJson.OptionalString(eventData, "message_id"),
            SourceBroadcasterUserId = EventSubJson.OptionalString(eventData, "source_broadcaster_user_id"),
            HasSourceMetadata = true,
            BroadcasterUserLogin = EventSubJson.RequireString(eventData, "broadcaster_user_login"),
            BroadcasterUserName = EventSubJson.RequireString(eventData, "broadcaster_user_name"),
            ChatterUserId = EventSubJson.RequireString(eventData, "chatter_user_id"),
            ChatterUserLogin = EventSubJson.RequireString(eventData, "chatter_user_login"),
            ChatterUserName = EventSubJson.RequireString(eventData, "chatter_user_name"),
            MessageText = messageText,
            CharacterCount = messageText.Length,
            MessageTimestamp = DateTime.UtcNow // EventSub doesn't provide message timestamp, use current time
        };
    }

    private static ChannelUpdateEventContract BuildChannelUpdate(JsonElement eventData, string messageId) => new()
    {
        MessageId = messageId,
        BroadcasterUserId = EventSubJson.RequireNumericId(eventData, "broadcaster_user_id"),
        BroadcasterUserLogin = EventSubJson.RequireString(eventData, "broadcaster_user_login"),
        BroadcasterUserName = EventSubJson.RequireString(eventData, "broadcaster_user_name"),
        // Twitch sends empty strings when the title or category is unset; null is tolerated the same way.
        StreamTitle = EventSubJson.OptionalString(eventData, "title") ?? string.Empty,
        Language = EventSubJson.OptionalString(eventData, "language") ?? string.Empty,
        CategoryId = EventSubJson.OptionalString(eventData, "category_id") ?? string.Empty,
        CategoryName = EventSubJson.OptionalString(eventData, "category_name") ?? string.Empty
    };

    /// <summary>
    /// Checks the Twitch HMAC-SHA256 signature over message id + timestamp + raw body in constant time.
    /// </summary>
    public static bool IsSignatureValid(string secret, string messageId, string messageTimestamp, string requestBody, string signatureHeader)
    {
        if (string.IsNullOrEmpty(secret) ||
            !signatureHeader.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[] providedHash;
        try
        {
            providedHash = Convert.FromHexString(signatureHeader.AsSpan(SignaturePrefix.Length));
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedHash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(messageId + messageTimestamp + requestBody));

        return CryptographicOperations.FixedTimeEquals(expectedHash, providedHash);
    }

    /// <summary>
    /// Parses the RFC 3339 timestamp header as an absolute instant, independent of the server time zone.
    /// </summary>
    public static bool TryParseTimestamp(string value, out DateTimeOffset timestamp) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out timestamp);

    public static bool IsTimestampFresh(DateTimeOffset sentAt, DateTimeOffset now, TimeSpan maxAge)
    {
        var age = now - sentAt;
        return age <= maxAge && age >= -AllowedClockSkew;
    }
}
