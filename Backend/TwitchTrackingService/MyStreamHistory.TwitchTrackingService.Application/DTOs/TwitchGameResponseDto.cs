using System.Text.Json.Serialization;

namespace MyStreamHistory.TwitchTrackingService.Application.DTOs;

public class TwitchGameResponseDto
{
    [JsonPropertyName("data")]
    public List<TwitchGameDataDto> Data { get; set; } = new();
}

public class TwitchGameDataDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;
    
    [JsonPropertyName("box_art_url")]
    public string BoxArtUrl { get; set; } = null!;
    
    [JsonPropertyName("igdb_id")]
    public string? IgdbId { get; set; }
}

