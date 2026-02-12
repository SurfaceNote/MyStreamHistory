namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface IStreamCategoryService
{
    Task<(Guid StreamSessionId, Guid? StreamCategoryId)?> GetActiveStreamCategoryAsync(string twitchUserId, CancellationToken cancellationToken = default);
}

