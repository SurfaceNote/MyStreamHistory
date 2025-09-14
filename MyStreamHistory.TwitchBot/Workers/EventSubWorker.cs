using System.Text.Json;
using MyStreamHistory.TwitchBot.Services;

namespace MyStreamHistory.TwitchBot.Workers;

public class EventSubWorker(
    IHttpClientFactory httpClientFactory, 
    TwitchAuthService twitchAuthService,
    IConfiguration config,
    ILogger<EventSubWorker> logger) 
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var twitchClient = httpClientFactory.CreateClient("TwitchApi");

                var streamers = new List<string>()
                {
                    "57456812", "57361888", "36620767", "43182504", "169112301", "762609330", "43510738", "1220778572"
                };
                
                await twitchAuthService.GetAppAccessTokenAsync(stoppingToken);
                

                foreach (var streamerId in streamers)
                {
                    var checkResponse =
                        await twitchClient.GetAsync($"eventsub/subscriptions?broadcaster_user_id={streamerId}",
                            stoppingToken);
                    if (!checkResponse.IsSuccessStatusCode)
                    {
                        logger.LogWarning($"Failed to check subscription for {streamerId}.");
                        continue;
                    }
                    
                    using var checkStream = await checkResponse.Content.ReadAsStreamAsync(stoppingToken);
                    using var doc = await JsonDocument.ParseAsync(checkStream, cancellationToken: stoppingToken);
                
                    if (doc.RootElement.GetProperty("data").GetArrayLength() < -1)
                    {
                        foreach (var eventType in new[] { "stream.online", "stream.offline" })
                        {
                            var callback = config["Twitch:EventSubCallbackUrl"];
                            var secret = config["Twitch:EventSubSecret"];
                            var payload = new
                            {
                                type = eventType,
                                version = "1",
                                condition = new { broadcaster_user_id = streamerId },
                                transport = new
                                {
                                    method = "webhook",
                                    callback = callback,
                                    secret = secret,
                                }
                            };
                
                            var response =
                                await twitchClient.PostAsJsonAsync("eventsub/subscriptions", payload,
                                    stoppingToken);
                            using var checkStream1 = await response.Content.ReadAsStreamAsync(stoppingToken);
                            using var doc1 =
                                await JsonDocument.ParseAsync(checkStream1, cancellationToken: stoppingToken);
                
                            logger.LogInformation(
                                $"Subscribed {streamerId} for {eventType} with status {response.StatusCode}.");
                            
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occured while checking subscriptions.");
            }
            
            await Task.Delay(TimeSpan.FromHours(100), stoppingToken);
        }
    }
}