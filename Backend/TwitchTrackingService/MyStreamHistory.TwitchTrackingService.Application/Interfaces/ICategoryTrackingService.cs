namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface ICategoryTrackingService
{
    Task ProcessStreamCategoriesAsync(Dictionary<Guid, string> streamGameIds, CancellationToken cancellationToken = default);
}

