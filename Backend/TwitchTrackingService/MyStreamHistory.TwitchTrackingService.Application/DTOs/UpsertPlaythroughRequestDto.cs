namespace MyStreamHistory.TwitchTrackingService.Application.DTOs;

public class UpsertPlaythroughRequestDto
{
    public Guid? Id { get; set; }
    public Guid TwitchCategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool AutoAddNewStreams { get; set; }
    public List<Guid> StreamCategoryIds { get; set; } = new();
}
