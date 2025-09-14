using System.Security.Cryptography;
using System.Text;

namespace MyStreamHistory.TwitchBot.Middleware;

public class TwitchEventSubMiddleware(
    RequestDelegate next, 
    IConfiguration config, 
    ILogger<TwitchEventSubMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals("/twitch/eventsub/callback", StringComparison.OrdinalIgnoreCase))
        {
            if (!context.Request.Headers.TryGetValue("Twitch-Eventsub-Message-Signature", out var signature) ||
                !context.Request.Headers.TryGetValue("Twitch-Eventsub-Message-Id", out var messageId) ||
                !context.Request.Headers.TryGetValue("Twitch-Eventsub-Message-Timestamp", out var timestamp))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Missing headers");
                return;
            }

            var secret = config["Twitch:EventSubSecret"];
            if (!string.IsNullOrEmpty(secret))
            {
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
                var payload = messageId + timestamp + body;
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var computed = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
                
                if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(computed), 
                        Encoding.UTF8.GetBytes(signature)))
                {
                    logger.LogWarning("Invalid signature for Twitch EventSub message {MessageId}", messageId);
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync("Invalid signature");
                    return;
                }
            }

            await next(context);
        }
    }
    
}