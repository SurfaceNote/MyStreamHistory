namespace MyStreamHistory.ViewerService.Application.DTOs;

public class UniqueViewerCountQueryDto
{
    public Guid PlaythroughId { get; set; }
    public IReadOnlyCollection<Guid> StreamCategoryIds { get; set; } = Array.Empty<Guid>();
}
