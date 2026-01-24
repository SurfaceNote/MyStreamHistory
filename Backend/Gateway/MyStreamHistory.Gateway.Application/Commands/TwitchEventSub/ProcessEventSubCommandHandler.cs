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

public class ProcessEventSubCommandHandler : IRequestHandler<ProcessEventSubCommand, string?>
{
    private readonly ITransportBus _transportBus;
    private readonly TwitchEventSubOptions _options;
    private readonly ILogger<ProcessEventSubCommandHandler> _logger;

    public ProcessEventSubCommandHandler(
        ITransportBus transportBus,
        IOptions<TwitchEventSubOptions> options,
        ILogger<ProcessEventSubCommandHandler> logger)
    {
        _transportBus = transportBus;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> Handle(ProcessEventSubCommand request, CancellationToken cancellationToken)
    {
        // Validate signature
        if (!ValidateSignature(request.RequestBody, request.MessageId, request.MessageTimestamp, request.MessageSignature))
        {
            _logger.LogWarning("Invalid Twitch EventSub signature");
            throw new AppException(ErrorCodes.PermissionDenied, "Invalid signature");
        }

        // Check message age
        if (!ValidateMessageAge(request.MessageTimestamp))
        {
            _logger.LogWarning("Twitch EventSub message too old");
            throw new AppException(ErrorCodes.PermissionDenied, "Message too old");
        }

        var jsonDocument = JsonDocument.Parse(request.RequestBody);
        var root = jsonDocument.RootElement;

        switch (request.MessageType)
        {
            // Handle webhook challenge (verification)
            case "webhook_callback_verification" when root.TryGetProperty("challenge", out var challenge):
            {
                var challengeValue = challenge.GetString();
                _logger.LogInformation("Responding to Twitch EventSub challenge");
                return challengeValue;
            }
            // Handle notification
            case "notification":
            {
                if (!root.TryGetProperty("subscription", out var subscription) ||
                    !root.TryGetProperty("event", out var eventData))
                {
                    _logger.LogWarning("Invalid EventSub notification format");
                    throw new AppException(ErrorCodes.InternalError, "Invalid notification format");
                }

                var subscriptionType = subscription.GetProperty("type").GetString();

                switch (subscriptionType)
                {
                    case "stream.online":
                        await HandleStreamOnlineAsync(eventData, cancellationToken);
                        break;
                    case "stream.offline":
                        await HandleStreamOfflineAsync(eventData, cancellationToken);
                        break;
                    default:
                        _logger.LogWarning("Unknown EventSub subscription type: {Type}", subscriptionType);
                        break;
                }

                break;
            }
        }

        return null;
    }

    private async Task HandleStreamOnlineAsync(JsonElement eventData, CancellationToken cancellationToken)
    {
        var eventContract = new StreamOnlineEventContract
        {
            BroadcasterUserId = int.Parse(eventData.GetProperty("broadcaster_user_id").GetString()!),
            BroadcasterUserLogin = eventData.GetProperty("broadcaster_user_login").GetString()!,
            BroadcasterUserName = eventData.GetProperty("broadcaster_user_name").GetString()!,
            StartedAt = eventData.GetProperty("started_at").GetDateTime(),
            Type = eventData.GetProperty("type").GetString()!
        };

        _logger.LogInformation("Publishing stream.online event for {BroadcasterUserLogin}", eventContract.BroadcasterUserLogin);
        await _transportBus.PublishAsync(eventContract, cancellationToken);
    }

    private async Task HandleStreamOfflineAsync(JsonElement eventData, CancellationToken cancellationToken)
    {
        var eventContract = new StreamOfflineEventContract
        {
            BroadcasterUserId = int.Parse(eventData.GetProperty("broadcaster_user_id").GetString()!),
            BroadcasterUserLogin = eventData.GetProperty("broadcaster_user_login").GetString()!,
            BroadcasterUserName = eventData.GetProperty("broadcaster_user_name").GetString()!
        };

        _logger.LogInformation("Publishing stream.offline event for {BroadcasterUserLogin}", eventContract.BroadcasterUserLogin);
        await _transportBus.PublishAsync(eventContract, cancellationToken);
    }

    private bool ValidateSignature(string requestBody, string messageId, string messageTimestamp, string messageSignature)
    {
        var message = messageId + messageTimestamp + requestBody;
        var secretBytes = Encoding.UTF8.GetBytes(_options.Secret);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(secretBytes);
        var hash = hmac.ComputeHash(messageBytes);
        var computedSignature = "sha256=" + BitConverter.ToString(hash).Replace("-", "").ToLower();

        return computedSignature == messageSignature;
    }

    private bool ValidateMessageAge(string messageTimestamp)
    {
        if (!DateTime.TryParse(messageTimestamp, out var timestamp))
        {
            return false;
        }

        var age = DateTime.UtcNow - timestamp;
        return age.TotalMinutes <= _options.MaxAgeMinutes;
    }
}

