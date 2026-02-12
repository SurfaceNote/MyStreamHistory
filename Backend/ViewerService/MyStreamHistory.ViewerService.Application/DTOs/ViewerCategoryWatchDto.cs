namespace MyStreamHistory.ViewerService.Application.DTOs;

public class ViewerCategoryWatchDto
{
    public Guid Id { get; set; }
    public Guid ViewerId { get; set; }
    public Guid StreamCategoryId { get; set; }
    public int MinutesWatched { get; set; }
    public decimal ChatPoints { get; set; }
    public decimal Experience { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}

