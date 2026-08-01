namespace MyStreamHistory.ViewerService.Application.DTOs;

public class ActiveStreamCategoryDto
{
    public Guid StreamSessionId { get; set; }
    public Guid? StreamCategoryId { get; set; }
    public bool IsLiveConfirmed { get; set; }
}
