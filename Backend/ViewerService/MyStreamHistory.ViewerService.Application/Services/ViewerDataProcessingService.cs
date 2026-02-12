using Microsoft.Extensions.Logging;
using MyStreamHistory.ViewerService.Application.DTOs;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Application.Services;

public class ViewerDataProcessingService : IViewerDataProcessingService
{
    private readonly ITwitchChatApiClient _chatApiClient;
    private readonly ITwitchUsersApiClient _usersApiClient;
    private readonly IAuthTokenService _authTokenService;
    private readonly IViewerRepository _viewerRepository;
    private readonly IViewerCategoryWatchRepository _categoryWatchRepository;
    private readonly IViewerStatsRepository _viewerStatsRepository;
    private readonly ILogger<ViewerDataProcessingService> _logger;
    
    // Sync viewer data if not synced for 7 days
    private static readonly TimeSpan DataSyncInterval = TimeSpan.FromDays(7);

    public ViewerDataProcessingService(
        ITwitchChatApiClient chatApiClient,
        ITwitchUsersApiClient usersApiClient,
        IAuthTokenService authTokenService,
        IViewerRepository viewerRepository,
        IViewerCategoryWatchRepository categoryWatchRepository,
        IViewerStatsRepository viewerStatsRepository,
        ILogger<ViewerDataProcessingService> logger)
    {
        _chatApiClient = chatApiClient;
        _usersApiClient = usersApiClient;
        _authTokenService = authTokenService;
        _viewerRepository = viewerRepository;
        _categoryWatchRepository = categoryWatchRepository;
        _viewerStatsRepository = viewerStatsRepository;
        _logger = logger;
    }

    public async Task ProcessSnapshotAsync(DataCollectionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Processing snapshot for {Count} streams at {Timestamp}", snapshot.StreamSnapshots.Count, snapshot.Timestamp);

        foreach (var (twitchUserId, streamSnapshot) in snapshot.StreamSnapshots)
        {
            try
            {
                await ProcessStreamSnapshotAsync(twitchUserId, streamSnapshot, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing snapshot for TwitchUserId: {TwitchUserId}", twitchUserId);
            }
        }
    }

    private async Task ProcessStreamSnapshotAsync(string twitchUserId, StreamDataSnapshot streamSnapshot, CancellationToken cancellationToken)
    {
        if (!streamSnapshot.CurrentCategoryId.HasValue)
        {
            _logger.LogWarning("No category for stream TwitchUserId: {TwitchUserId}, skipping", twitchUserId);
            return;
        }

        // Get access token
        var tokenResult = await _authTokenService.GetTwitchAccessTokenAsync(twitchUserId, cancellationToken);
        if (tokenResult == null)
        {
            _logger.LogError("Failed to get access token for TwitchUserId: {TwitchUserId}", twitchUserId);
            return;
        }

        var (accessToken, _) = tokenResult.Value;

        // Get list of current chatters (with retry on 401)
        List<TwitchChatterDto> chatters;
        try
        {
            chatters = await _chatApiClient.GetChattersAsync(twitchUserId, accessToken, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Unauthorized") || ex.Message.Contains("401"))
        {
            _logger.LogWarning("Received 401 Unauthorized for TwitchUserId: {TwitchUserId}, attempting token refresh", twitchUserId);
            
            // Try to refresh the token
            var refreshResult = await _authTokenService.RefreshTwitchAccessTokenAsync(twitchUserId, cancellationToken);
            if (refreshResult == null)
            {
                _logger.LogError("Failed to refresh token for TwitchUserId: {TwitchUserId}", twitchUserId);
                return;
            }

            var (newAccessToken, _) = refreshResult.Value;
            
            // Retry with new token
            try
            {
                chatters = await _chatApiClient.GetChattersAsync(twitchUserId, newAccessToken, cancellationToken);
                _logger.LogInformation("Successfully retrieved chatters after token refresh for TwitchUserId: {TwitchUserId}", twitchUserId);
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "Failed to get chatters even after token refresh for TwitchUserId: {TwitchUserId}", twitchUserId);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get chatters for TwitchUserId: {TwitchUserId}", twitchUserId);
            return;
        }

        // Prepare viewer data
        var viewerData = chatters.Select(c => (c.UserId, c.UserLogin, c.UserName)).ToList();
        var viewers = await _viewerRepository.GetOrCreateViewersAsync(viewerData, cancellationToken);

        // Check if we need to sync viewer data (for viewers that haven't been synced or synced long ago)
        await SyncViewerDataIfNeededAsync(viewers, accessToken, cancellationToken);

        // Prepare updates
        var categoryUpdates = new List<ViewerCategoryWatch>();
        var statsUpdates = new List<ViewerStats>();
        var now = DateTime.UtcNow;

        foreach (var viewer in viewers)
        {
            // Get existing watch record
            var watch = await _categoryWatchRepository.GetByViewerAndCategoryAsync(viewer.Id, streamSnapshot.CurrentCategoryId.Value, cancellationToken);
            
            if (watch == null)
            {
                watch = new ViewerCategoryWatch
                {
                    Id = Guid.NewGuid(),
                    ViewerId = viewer.Id,
                    StreamCategoryId = streamSnapshot.CurrentCategoryId.Value,
                    MinutesWatched = 0,
                    ChatPoints = 0,
                    Experience = 0,
                    LastUpdatedAt = now
                };
            }

            // Increment minutes watched
            watch.MinutesWatched += 1;

            // Add chat points if viewer sent messages
            var chatPointsToAdd = 0m;
            if (streamSnapshot.ChatMessages.TryGetValue(viewer.TwitchUserId, out var characterCount))
            {
                chatPointsToAdd = (decimal)characterCount / 140;
                watch.ChatPoints += chatPointsToAdd;
            }

            // Calculate experience for category watch
            watch.Experience = Math.Round(watch.ChatPoints / 4 + (decimal)watch.MinutesWatched / 30, 2);
            watch.LastUpdatedAt = now;
            categoryUpdates.Add(watch);

            // Get or create viewer stats (aggregated by streamer)
            var stats = await _viewerStatsRepository.GetByViewerAndStreamerAsync(viewer.Id, twitchUserId, cancellationToken);
            
            if (stats == null)
            {
                stats = new ViewerStats
                {
                    Id = Guid.NewGuid(),
                    ViewerId = viewer.Id,
                    StreamerTwitchUserId = twitchUserId,
                    MinutesWatched = 0,
                    EarnedMsgPoints = 0,
                    Experience = 0,
                    LastUpdatedAt = now
                };
            }

            // Update aggregated stats
            stats.MinutesWatched += 1;
            stats.EarnedMsgPoints += chatPointsToAdd;
            stats.Experience = Math.Round(stats.EarnedMsgPoints / 4 + (decimal)stats.MinutesWatched / 30, 2);
            stats.LastUpdatedAt = now;
            statsUpdates.Add(stats);
        }

        // Bulk upsert category watches
        if (categoryUpdates.Any())
        {
            await _categoryWatchRepository.BulkUpsertAsync(categoryUpdates, cancellationToken);
            _logger.LogInformation("Updated {Count} viewer category records for TwitchUserId: {TwitchUserId}", categoryUpdates.Count, twitchUserId);
        }

        // Bulk upsert viewer stats
        if (statsUpdates.Any())
        {
            await _viewerStatsRepository.BulkUpsertAsync(statsUpdates, cancellationToken);
            _logger.LogInformation("Updated {Count} viewer stats records for TwitchUserId: {TwitchUserId}", statsUpdates.Count, twitchUserId);
        }
    }

    private async Task SyncViewerDataIfNeededAsync(List<Viewer> viewers, string accessToken, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        
        // Find viewers that need data sync (never synced or synced more than DataSyncInterval ago)
        var viewersToSync = viewers
            .Where(v => !v.LastDataSyncAt.HasValue || (now - v.LastDataSyncAt.Value) > DataSyncInterval)
            .ToList();

        if (!viewersToSync.Any())
        {
            _logger.LogDebug("No viewers need data sync");
            return;
        }

        _logger.LogInformation("Syncing data for {Count} viewers", viewersToSync.Count);

        // Twitch API allows max 100 users per request, so we need to batch
        var batchSize = 100;
        var batches = viewersToSync
            .Select((v, i) => new { Viewer = v, Index = i })
            .GroupBy(x => x.Index / batchSize)
            .Select(g => g.Select(x => x.Viewer).ToList())
            .ToList();

        var updatedViewers = new List<Viewer>();

        foreach (var batch in batches)
        {
            try
            {
                var userIds = batch.Select(v => v.TwitchUserId).ToList();
                var twitchUsers = await _usersApiClient.GetUsersByIdsAsync(userIds, accessToken, cancellationToken);

                // Create a dictionary for quick lookup
                var twitchUsersDict = twitchUsers.ToDictionary(u => u.Id);

                foreach (var viewer in batch)
                {
                    if (twitchUsersDict.TryGetValue(viewer.TwitchUserId, out var twitchUser))
                    {
                        // Check if data has changed
                        var hasChanges = false;

                        if (viewer.Login != twitchUser.Login)
                        {
                            _logger.LogInformation("Updating login for viewer {TwitchUserId}: {OldLogin} -> {NewLogin}", 
                                viewer.TwitchUserId, viewer.Login, twitchUser.Login);
                            viewer.Login = twitchUser.Login;
                            hasChanges = true;
                        }

                        if (viewer.DisplayName != twitchUser.DisplayName)
                        {
                            _logger.LogInformation("Updating display name for viewer {TwitchUserId}: {OldName} -> {NewName}", 
                                viewer.TwitchUserId, viewer.DisplayName, twitchUser.DisplayName);
                            viewer.DisplayName = twitchUser.DisplayName;
                            hasChanges = true;
                        }

                        if (viewer.ProfileImageUrl != twitchUser.ProfileImageUrl)
                        {
                            _logger.LogDebug("Updating profile image for viewer {TwitchUserId}", viewer.TwitchUserId);
                            viewer.ProfileImageUrl = twitchUser.ProfileImageUrl;
                            hasChanges = true;
                        }

                        // Always update LastDataSyncAt
                        viewer.LastDataSyncAt = now;

                        if (hasChanges || !viewer.LastDataSyncAt.HasValue)
                        {
                            updatedViewers.Add(viewer);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Could not find Twitch user data for viewer {TwitchUserId}", viewer.TwitchUserId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing viewer data for batch");
            }
        }

        // Bulk update all changed viewers
        if (updatedViewers.Any())
        {
            await _viewerRepository.BulkUpdateAsync(updatedViewers, cancellationToken);
            _logger.LogInformation("Successfully synced data for {Count} viewers", updatedViewers.Count);
        }
    }
}

