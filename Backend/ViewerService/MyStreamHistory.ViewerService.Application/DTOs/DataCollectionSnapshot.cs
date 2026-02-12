using System.Collections.Concurrent;

namespace MyStreamHistory.ViewerService.Application.DTOs;

public class DataCollectionSnapshot
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, StreamDataSnapshot> StreamSnapshots { get; set; } = new();
}

public class StreamDataSnapshot
{
    public string TwitchUserId { get; set; } = string.Empty;
    public Guid StreamSessionId { get; set; }
    public Guid? CurrentCategoryId { get; set; }
    public Dictionary<string, int> ChatMessages { get; set; } = new(); // ViewerId -> character count
}

