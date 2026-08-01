namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface IStreamLifecycleLock
{
    ValueTask<IAsyncDisposable> AcquireAsync(
        string twitchUserId,
        CancellationToken cancellationToken = default);
}
