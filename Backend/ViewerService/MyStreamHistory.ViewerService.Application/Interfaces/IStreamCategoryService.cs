using MyStreamHistory.ViewerService.Application.DTOs;

namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface IStreamCategoryService
{
    Task<ActiveStreamCategoryDto?> GetActiveStreamCategoryAsync(
        string twitchUserId,
        CancellationToken cancellationToken = default);
}

