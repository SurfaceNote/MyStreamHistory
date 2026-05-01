namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface IActiveStreamBootstrapService
{
    Task BootstrapActiveStreamsAsync(CancellationToken cancellationToken = default);
}
