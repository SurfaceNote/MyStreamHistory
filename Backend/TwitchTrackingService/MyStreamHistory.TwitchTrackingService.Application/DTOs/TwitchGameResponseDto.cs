namespace MyStreamHistory.TwitchTrackingService.Application.DTOs;

public class TwitchGameResponseDto
{
    public List<TwitchGameDataDto> Data { get; set; } = new();
}

public class TwitchGameDataDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Box_Art_Url { get; set; } = null!;
    public string? Igdb_Id { get; set; }
}

