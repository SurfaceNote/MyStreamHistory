namespace MyStreamHistory.Gateway.Application.DTOs;

public class PlaythroughSettingsDto
{
    public List<PlaythroughDto> Playthroughs { get; set; } = new();
    public List<PlaythroughGameOptionDto> AvailableGames { get; set; } = new();
}
