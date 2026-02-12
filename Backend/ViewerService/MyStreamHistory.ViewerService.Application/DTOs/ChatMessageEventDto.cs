namespace MyStreamHistory.ViewerService.Application.DTOs;

public class ChatMessageEventDto
{
    public string ChatterUserId { get; set; } = string.Empty;
    public string ChatterUserLogin { get; set; } = string.Empty;
    public string ChatterUserName { get; set; } = string.Empty;
    public int CharacterCount { get; set; }
}

