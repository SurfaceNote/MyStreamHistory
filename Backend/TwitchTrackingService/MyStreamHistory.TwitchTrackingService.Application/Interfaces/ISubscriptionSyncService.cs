namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface ISubscriptionSyncService
{
    Task SyncSubscriptionsAsync(CancellationToken cancellationToken = default);
}

