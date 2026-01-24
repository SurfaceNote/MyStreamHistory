using System.Text.Json.Serialization;

namespace MyStreamHistory.TwitchTrackingService.Application.DTOs;

/// <summary>
/// Represents detailed information about an EventSub subscription
/// </summary>
public class EventSubSubscriptionDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("cost")]
    public int Cost { get; set; }

    [JsonPropertyName("condition")]
    public EventSubConditionDto Condition { get; set; } = new();

    [JsonPropertyName("transport")]
    public EventSubTransportDto Transport { get; set; } = new();

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

public class EventSubConditionDto
{
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterUserId { get; set; } = string.Empty;
}

public class EventSubTransportDto
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("callback")]
    public string Callback { get; set; } = string.Empty;
}

public class EventSubSubscriptionsDto
{
    [JsonPropertyName("data")]
    public List<EventSubSubscriptionDto> Data { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("total_cost")]
    public int TotalCost { get; set; }

    [JsonPropertyName("max_total_cost")]
    public int MaxTotalCost { get; set; }
}

