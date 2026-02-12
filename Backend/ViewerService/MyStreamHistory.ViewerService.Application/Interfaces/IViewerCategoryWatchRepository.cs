using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface IViewerCategoryWatchRepository
{
    Task<ViewerCategoryWatch?> GetByViewerAndCategoryAsync(Guid viewerId, Guid streamCategoryId, CancellationToken cancellationToken = default);
    Task<List<ViewerCategoryWatch>> GetByViewerIdAsync(Guid viewerId, CancellationToken cancellationToken = default);
    Task<List<ViewerCategoryWatch>> GetByStreamCategoryIdAsync(Guid streamCategoryId, CancellationToken cancellationToken = default);
    Task<List<ViewerCategoryWatch>> GetByStreamCategoryIdsAsync(List<Guid> streamCategoryIds, CancellationToken cancellationToken = default);
    Task BulkUpsertAsync(List<ViewerCategoryWatch> watches, CancellationToken cancellationToken = default);
}

