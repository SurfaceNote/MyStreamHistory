namespace MyStreamHistory.TwitchTrackingService.Domain.Entities;

public class PlaythroughStreamCategory
{
    public Guid PlaythroughId { get; set; }
    public Guid StreamCategoryId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Playthrough Playthrough { get; set; } = null!;
    public StreamCategory StreamCategory { get; set; } = null!;
}
