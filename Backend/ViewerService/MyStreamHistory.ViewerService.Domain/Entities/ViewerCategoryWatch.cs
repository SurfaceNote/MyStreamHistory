namespace MyStreamHistory.ViewerService.Domain.Entities;

public class ViewerCategoryWatch
{
    public Guid Id { get; set; }
    public Guid ViewerId { get; set; }
    public Guid StreamCategoryId { get; set; }
    public int MinutesWatched { get; set; }
    public decimal ChatPoints { get; set; }
    public decimal Experience { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    
    // Navigation properties
    public Viewer Viewer { get; set; } = null!;
}

