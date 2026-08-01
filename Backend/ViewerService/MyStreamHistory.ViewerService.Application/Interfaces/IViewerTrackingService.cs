namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface IViewerTrackingService
{
    Task HandleStreamOnlineAsync(string twitchUserId, Guid streamSessionId, Guid? currentCategoryId, CancellationToken cancellationToken = default);
    Task HandleStreamOfflineAsync(string twitchUserId, Guid streamSessionId, CancellationToken cancellationToken = default);
}

