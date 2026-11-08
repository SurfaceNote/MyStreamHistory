using Microsoft.Extensions.Caching.Memory;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.ProductActivity;

namespace MyStreamHistory.Gateway.Api.ProductActivity;

/// <summary>
/// Publishes channel product activity (owner visits, overlay and panel usage) for metrics.
/// Each channel and kind is published at most once per hour per Gateway instance.
/// Recording never fails the request that triggered it.
/// </summary>
public sealed class ChannelActivityRecorder(
    ITransportBus bus,
    IMemoryCache cache,
    ILogger<ChannelActivityRecorder> logger)
{
    private static readonly TimeSpan DeduplicationWindow = TimeSpan.FromHours(1);

    public async Task RecordAsync(int twitchUserId, string kind, CancellationToken cancellationToken = default)
    {
        if (twitchUserId <= 0 || !ChannelActivityKinds.All.Contains(kind))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var key = $"channel-activity:{twitchUserId}:{kind}:{now:yyyyMMddHH}";
        if (cache.TryGetValue(key, out _))
        {
            return;
        }

        cache.Set(key, true, DeduplicationWindow + TimeSpan.FromMinutes(5));

        try
        {
            await bus.PublishAsync(new ChannelActivityObservedContract
            {
                TwitchUserId = twitchUserId,
                Kind = kind,
                OccurredAt = now
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            cache.Remove(key);
            logger.LogWarning(exception, "Failed to publish {Kind} activity for channel {TwitchUserId}", kind, twitchUserId);
        }
    }

    /// <summary>Requests from the Twitch panel extension are served from an *.ext-twitch.tv origin.</summary>
    public static bool IsTwitchExtensionRequest(HttpRequest request) =>
        Uri.TryCreate(request.Headers.Origin.ToString(), UriKind.Absolute, out var origin)
        && origin.Scheme == Uri.UriSchemeHttps
        && origin.Host.EndsWith(".ext-twitch.tv", StringComparison.OrdinalIgnoreCase);
}
