using MyStreamHistory.ViewerService.Application.DTOs;

namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface IViewerDataProcessingService
{
    Task ProcessSnapshotAsync(DataCollectionSnapshot snapshot, CancellationToken cancellationToken = default);
}

