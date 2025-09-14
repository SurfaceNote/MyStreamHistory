using System.Text;
using System.Text.Json;
using MyStreamHistory.TwitchBot.DTOs;

namespace MyStreamHistory.TwitchBot.Services;

public interface ITwitchEventDispatcher
{
    Task DispatchAsync(TwitchEventSubNotificationDto payload, CancellationToken cancellationToken = default);
    Task StreamOnlineAsync(JsonElement eventData);
    Task StreamOfflineAsync(JsonElement eventData);
}

public class TwitchEventDispatcher(
    IHttpClientFactory httpClientFactory,
    ILogger<TwitchEventDispatcher> logger) 
    :  ITwitchEventDispatcher
{
    public Task DispatchAsync(
        TwitchEventSubNotificationDto payload,
        CancellationToken cancellationToken = default)
    {
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));

        if (!string.IsNullOrWhiteSpace(payload.Challenge))
        {
            logger.LogInformation("Received challenge {Challenge}", payload.Challenge);
            return Task.CompletedTask;
        }

        logger.LogInformation(
            "EventSub notification: {Subscription} {Event}",
            payload.Subscription,
            payload.Event);

        // TODO: Route payload.Event to appropriate handlers.
        return Task.CompletedTask;
    }

    public async Task StreamOnlineAsync(JsonElement eventData)
    {
        logger.LogInformation($"Received stream.online event: {eventData}");
    }

    public async Task StreamOfflineAsync(JsonElement eventData)
    {
        logger.LogInformation($"Received stream.offline event: {eventData}");
    }

    private async Task ForwardAsync(string path, JsonElement eventData)
    {
        var client = httpClientFactory.CreateClient("Backend");
        var content = new StringContent(eventData.GetRawText(), Encoding.UTF8, "application/json");

        try
        {
            await client.PostAsync(path, content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to forward Twitch event to backend");
        }
    }
}