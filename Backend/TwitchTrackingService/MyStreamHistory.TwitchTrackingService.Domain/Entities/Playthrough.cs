namespace MyStreamHistory.TwitchTrackingService.Domain.Entities;

public class Playthrough
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TwitchUserId { get; set; }
    public Guid TwitchCategoryId { get; set; }
    public string Title { get; set; } = null!;
    public PlaythroughStatus Status { get; set; }
    public bool AutoAddNewStreams { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public TwitchCategory TwitchCategory { get; set; } = null!;
    public ICollection<PlaythroughStreamCategory> PlaythroughStreamCategories { get; set; } = new List<PlaythroughStreamCategory>();
}
