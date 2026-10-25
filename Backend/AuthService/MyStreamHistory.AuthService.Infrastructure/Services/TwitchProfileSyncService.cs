using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Infrastructure.Persistence;

namespace MyStreamHistory.AuthService.Infrastructure.Services;

public sealed class TwitchProfileSyncService(
    AuthDbContext dbContext,
    ITwitchUsersApiClient twitchUsersApiClient,
    ILogger<TwitchProfileSyncService> logger) : ITwitchProfileSyncService
{
    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        var ids = await dbContext.Users.AsNoTracking()
            .OrderBy(user => user.TwitchId)
            .Select(user => user.TwitchId)
            .ToArrayAsync(cancellationToken);
        var updatedCount = 0;
        var skippedCount = 0;
        var failedBatches = 0;

        foreach (var batch in ids.Chunk(100))
        {
            try
            {
                var profiles = await twitchUsersApiClient.GetUsersAsync(batch, cancellationToken);
                // Load after the network call; change tracking updates only profile columns.
                var users = await dbContext.Users.Where(user => batch.Contains(user.TwitchId))
                    .ToDictionaryAsync(user => user.TwitchId, cancellationToken);
                var updatedIds = new HashSet<int>();
                var returnedIds = new HashSet<int>();

                foreach (var profile in profiles)
                {
                    if (profile is null || !int.TryParse(profile.Id, out var id) || !users.TryGetValue(id, out var user)
                        || !returnedIds.Add(id))
                        continue;

                    if (string.IsNullOrWhiteSpace(profile.Login) || profile.Login.Length > 50
                        || string.IsNullOrWhiteSpace(profile.DisplayName) || profile.DisplayName.Length > 50
                        || !Uri.TryCreate(profile.ProfileImageUrl, UriKind.Absolute, out var avatar)
                        || (avatar.Scheme != Uri.UriSchemeHttps && avatar.Scheme != Uri.UriSchemeHttp))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (user.Login == profile.Login && user.DisplayName == profile.DisplayName
                        && user.Avatar == profile.ProfileImageUrl)
                        continue;

                    user.Login = profile.Login;
                    user.DisplayName = profile.DisplayName;
                    user.Avatar = profile.ProfileImageUrl!;
                    updatedIds.Add(id);
                }

                skippedCount += users.Count - returnedIds.Count;
                await dbContext.SaveChangesAsync(cancellationToken);
                updatedCount += updatedIds.Count;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failedBatches++;
                logger.LogError(ex, "Failed to synchronize Twitch profile batch of {Count} users", batch.Length);
            }
            finally
            {
                // Failed tracked changes must not leak into the next batch's save.
                dbContext.ChangeTracker.Clear();
            }
        }

        logger.LogInformation(
            "Twitch profile sync completed: {CheckedCount} users, {UpdatedCount} updated, {SkippedCount} missing or invalid, {FailedBatches} failed batches",
            ids.Length, updatedCount, skippedCount, failedBatches);
    }
}
