namespace MyStreamHistory.ViewerService.Domain.Entities;

public class Viewer
{
    public Guid Id { get; set; }
    public string TwitchUserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastDataSyncAt { get; set; }
    
    // Navigation properties
    public ICollection<ViewerCategoryWatch> CategoryWatches { get; set; } = new List<ViewerCategoryWatch>();
    public ICollection<ViewerStats> Stats { get; set; } = new List<ViewerStats>();
}

