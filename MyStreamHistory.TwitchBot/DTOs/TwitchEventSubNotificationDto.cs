using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyStreamHistory.TwitchBot.DTOs;

public class TwitchEventSubNotificationDto
{
    [JsonPropertyName("challenge")]
    public string? Challenge { get; set; }
    
    [JsonPropertyName("subscription")]
    public JsonElement? Subscription { get; set; }
    
    [JsonPropertyName("event")]
    public JsonElement? Event { get; set; }
}