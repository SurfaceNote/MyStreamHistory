namespace MyStreamHistory.ViewerService.Domain.Entities;

public class ProcessedEventSubMessage
{
    public Guid Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}

