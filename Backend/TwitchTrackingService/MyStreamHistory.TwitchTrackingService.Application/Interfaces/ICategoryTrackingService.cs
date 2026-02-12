namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface ICategoryTrackingService
{
    Task ProcessStreamCategoriesAsync(Dictionary<Guid, string> streamGameIds, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Processes category change for a single stream and publishes StreamCategoryChangedEventContract event
    /// </summary>
    /// <returns>True if category changed, false if category remained the same</returns>
    Task<bool> ProcessSingleStreamCategoryAsync(Guid streamSessionId, string categoryId, string categoryName, CancellationToken cancellationToken = default);
}

