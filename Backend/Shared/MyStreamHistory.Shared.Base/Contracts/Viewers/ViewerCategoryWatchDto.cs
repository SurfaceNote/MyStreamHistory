namespace MyStreamHistory.Shared.Base.Contracts.Viewers;

public class ViewerCategoryWatchDto
{
    public Guid Id { get; set; }
    public Guid ViewerId { get; set; }
    public Guid StreamCategoryId { get; set; }
    public int MinutesWatched { get; set; }
    public decimal ChatPoints { get; set; }
    public decimal Experience { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    
    // Navigation properties for responses
    public ViewerDto? Viewer { get; set; }
    public string? CategoryName { get; set; }
}

