namespace MyStreamHistory.AuthService.Application.Interfaces;

public interface ITwitchProfileSyncService
{
    Task SyncAsync(CancellationToken cancellationToken = default);
}
