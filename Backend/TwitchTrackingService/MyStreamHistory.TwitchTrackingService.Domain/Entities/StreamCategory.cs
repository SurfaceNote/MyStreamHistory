namespace MyStreamHistory.TwitchTrackingService.Domain.Entities;

public class StreamCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StreamSessionId { get; set; }
    public Guid TwitchCategoryId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    
    // Navigation properties
    public StreamSession StreamSession { get; set; } = null!;
    public TwitchCategory TwitchCategory { get; set; } = null!;
}

