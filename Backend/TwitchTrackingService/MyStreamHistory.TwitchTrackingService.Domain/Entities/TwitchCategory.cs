namespace MyStreamHistory.TwitchTrackingService.Domain.Entities;

public class TwitchCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TwitchId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string BoxArtUrl { get; set; } = null!;
    public string? IgdbId { get; set; }
    
    // Navigation properties
    public ICollection<StreamCategory> StreamCategories { get; set; } = new List<StreamCategory>();
}

