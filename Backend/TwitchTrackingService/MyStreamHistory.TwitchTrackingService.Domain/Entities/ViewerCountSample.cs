namespace MyStreamHistory.TwitchTrackingService.Domain.Entities;

public class ViewerCountSample
{
    public Guid StreamSessionId { get; set; }
    public DateTime Minute { get; set; }
    public DateTime ObservedAt { get; set; }
    public int ViewerCount { get; set; }
}
