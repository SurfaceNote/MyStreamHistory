using MassTransit;
using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Services;

public class CategoryTrackingService : ICategoryTrackingService
{
    private readonly ITwitchCategoryRepository _categoryRepository;
    private readonly IStreamCategoryRepository _streamCategoryRepository;
    private readonly IStreamSessionRepository _streamSessionRepository;
    private readonly ITwitchApiClient _twitchApiClient;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CategoryTrackingService> _logger;

    public CategoryTrackingService(
        ITwitchCategoryRepository categoryRepository,
        IStreamCategoryRepository streamCategoryRepository,
        IStreamSessionRepository streamSessionRepository,
        ITwitchApiClient twitchApiClient,
        IPublishEndpoint publishEndpoint,
        ILogger<CategoryTrackingService> logger)
    {
        _categoryRepository = categoryRepository;
        _streamCategoryRepository = streamCategoryRepository;
        _streamSessionRepository = streamSessionRepository;
        _twitchApiClient = twitchApiClient;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task ProcessStreamCategoriesAsync(Dictionary<Guid, string> streamGameIds, CancellationToken cancellationToken = default)
    {
        if (streamGameIds == null || streamGameIds.Count == 0)
        {
            _logger.LogDebug("No stream game IDs to process");
            return;
        }

        // Step 1: Collect all unique game IDs
        var uniqueGameIds = streamGameIds.Values
            .Where(gameId => !string.IsNullOrWhiteSpace(gameId))
            .Distinct()
            .ToList();

        if (uniqueGameIds.Count == 0)
        {
            _logger.LogDebug("No valid game IDs found in streams");
            return;
        }

        _logger.LogInformation("Processing {UniqueGameCount} unique game IDs from {StreamCount} streams", 
            uniqueGameIds.Count, streamGameIds.Count);

        // Step 2: Check which categories already exist in DB
        var existingCategories = await _categoryRepository.GetByTwitchIdsAsync(uniqueGameIds, cancellationToken);
        var existingCategoryDict = existingCategories.ToDictionary(c => c.TwitchId, c => c);

        // Step 3: Find new categories that need to be fetched from Twitch API
        var newGameIds = uniqueGameIds
            .Where(id => !existingCategoryDict.ContainsKey(id))
            .ToList();

        if (newGameIds.Count > 0)
        {
            _logger.LogInformation("Fetching {NewGameCount} new categories from Twitch API", newGameIds.Count);

            try
            {
                var gameData = await _twitchApiClient.GetGamesAsync(newGameIds, cancellationToken);
                
                if (gameData.Count > 0)
                {
                    var newCategories = gameData.Select(game => new TwitchCategory
                    {
                        TwitchId = game.Id,
                        Name = game.Name,
                        BoxArtUrl = game.BoxArtUrl,
                        IgdbId = game.IgdbId
                    }).ToList();

                    await _categoryRepository.AddRangeAsync(newCategories, cancellationToken);
                    
                    // Add new categories to dictionary for use below
                    foreach (var category in newCategories)
                    {
                        existingCategoryDict[category.TwitchId] = category;
                    }

                    _logger.LogInformation("Saved {NewCategoryCount} new categories to database", newCategories.Count);
                }
                else
                {
                    _logger.LogWarning("Twitch API returned no data for {NewGameCount} game IDs", newGameIds.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching game data from Twitch API");
                // Continue with existing categories even if new ones failed
            }
        }

        // Step 4: Process each stream's category
        var currentTime = DateTime.UtcNow;
        int segmentsClosed = 0;
        int segmentsCreated = 0;
        int segmentsUpdated = 0;

        foreach (var (streamSessionId, gameId) in streamGameIds)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                _logger.LogDebug("Skipping stream {StreamSessionId} with empty game ID", streamSessionId);
                continue;
            }

            if (!existingCategoryDict.TryGetValue(gameId, out var category))
            {
                _logger.LogWarning("Category {GameId} not found for stream {StreamSessionId}, skipping", 
                    gameId, streamSessionId);
                continue;
            }

            try
            {
                // Get StreamSession to obtain BroadcasterUserId for event publishing
                var streamSession = await _streamSessionRepository.GetByIdAsync(streamSessionId, cancellationToken);
                if (streamSession == null)
                {
                    _logger.LogWarning("StreamSession {StreamSessionId} not found during polling", streamSessionId);
                    continue;
                }

                // Get active segment for this stream
                var activeSegment = await _streamCategoryRepository.GetActiveSegmentByStreamIdAsync(streamSessionId, cancellationToken);

                if (activeSegment == null)
                {
                    // No active segment - create new one
                    var newSegment = new StreamCategory
                    {
                        StreamSessionId = streamSessionId,
                        TwitchCategoryId = category.Id,
                        StartedAt = currentTime,
                        EndedAt = null  // Active segment has null EndedAt
                    };

                    await _streamCategoryRepository.CreateSegmentAsync(newSegment, cancellationToken);
                    segmentsCreated++;
                    
                    _logger.LogDebug("Created new segment for stream {StreamSessionId}, category {CategoryName}", 
                        streamSessionId, category.Name);

                    // Publish event with StreamCategory.Id (not TwitchCategory.Id)
                    await PublishCategoryChangedEventAsync(streamSessionId, streamSession.TwitchUserId, null, newSegment.Id, category.Name, currentTime, cancellationToken);
                }
                else if (activeSegment.TwitchCategoryId != category.Id)
                {
                    // Category changed - close old segment and create new one
                    var oldStreamCategoryId = activeSegment.Id; // Use StreamCategory.Id, not TwitchCategory.Id
                    
                    await _streamCategoryRepository.CloseSegmentAsync(activeSegment.Id, currentTime, cancellationToken);
                    segmentsClosed++;

                    var newSegment = new StreamCategory
                    {
                        StreamSessionId = streamSessionId,
                        TwitchCategoryId = category.Id,
                        StartedAt = currentTime,
                        EndedAt = null  // Active segment has null EndedAt
                    };

                    await _streamCategoryRepository.CreateSegmentAsync(newSegment, cancellationToken);
                    segmentsCreated++;
                    
                    _logger.LogInformation("Category changed for stream {StreamSessionId}: {OldCategory} -> {NewCategory}", 
                        streamSessionId, activeSegment.TwitchCategory?.Name ?? "Unknown", category.Name);

                    // Publish event with StreamCategory.Id (not TwitchCategory.Id)
                    await PublishCategoryChangedEventAsync(streamSessionId, streamSession.TwitchUserId, oldStreamCategoryId, newSegment.Id, category.Name, currentTime, cancellationToken);
                }
                else
                {
                    // Same category - no action needed, segment remains active with EndedAt = null
                    // Duration will be calculated dynamically: CurrentTime - StartedAt
                    segmentsUpdated++;
                    
                    _logger.LogDebug("Segment remains active for stream {StreamSessionId}, category {CategoryName}", 
                        streamSessionId, category.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing category for stream {StreamSessionId}", streamSessionId);
            }
        }

        _logger.LogInformation(
            "Category processing complete. Segments: {Created} created, {Closed} closed, {Updated} updated",
            segmentsCreated, segmentsClosed, segmentsUpdated);
    }

    public async Task<bool> ProcessSingleStreamCategoryAsync(Guid streamSessionId, string categoryId, string categoryName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            _logger.LogDebug("Empty category ID for stream {StreamSessionId}", streamSessionId);
            return false;
        }

        try
        {
            // Get StreamSession to obtain BroadcasterUserId
            var streamSession = await _streamSessionRepository.GetByIdAsync(streamSessionId, cancellationToken);
            if (streamSession == null)
            {
                _logger.LogWarning("StreamSession {StreamSessionId} not found", streamSessionId);
                return false;
            }
            // Check if category exists in DB
            var categories = await _categoryRepository.GetByTwitchIdsAsync(new List<string> { categoryId }, cancellationToken);
            var category = categories.FirstOrDefault();

            if (category == null)
            {
                // Fetch from Twitch API
                _logger.LogInformation("Fetching new category {CategoryId} from Twitch API", categoryId);
                
                var gameData = await _twitchApiClient.GetGamesAsync(new List<string> { categoryId }, cancellationToken);
                
                if (gameData.Count > 0)
                {
                    category = new TwitchCategory
                    {
                        TwitchId = gameData[0].Id,
                        Name = gameData[0].Name,
                        BoxArtUrl = gameData[0].BoxArtUrl,
                        IgdbId = gameData[0].IgdbId
                    };

                    await _categoryRepository.AddRangeAsync(new List<TwitchCategory> { category }, cancellationToken);
                    _logger.LogInformation("Saved new category {CategoryName} to database", category.Name);
                }
                else
                {
                    _logger.LogWarning("Twitch API returned no data for category {CategoryId}, using provided name", categoryId);
                    
                    // Create category with provided name
                    category = new TwitchCategory
                    {
                        TwitchId = categoryId,
                        Name = categoryName,
                        BoxArtUrl = string.Empty,
                        IgdbId = null
                    };
                    
                    await _categoryRepository.AddRangeAsync(new List<TwitchCategory> { category }, cancellationToken);
                }
            }

            // Get active segment for this stream
            var activeSegment = await _streamCategoryRepository.GetActiveSegmentByStreamIdAsync(streamSessionId, cancellationToken);
            var currentTime = DateTime.UtcNow;

            if (activeSegment == null)
            {
                // No active segment - create new one
                var newSegment = new StreamCategory
                {
                    StreamSessionId = streamSessionId,
                    TwitchCategoryId = category.Id,
                    StartedAt = currentTime,
                    EndedAt = null
                };

                await _streamCategoryRepository.CreateSegmentAsync(newSegment, cancellationToken);
                
                _logger.LogInformation("Created new category segment for stream {StreamSessionId}, category {CategoryName}", 
                    streamSessionId, category.Name);

                // Publish event with StreamCategory.Id (not TwitchCategory.Id)
                await PublishCategoryChangedEventAsync(streamSessionId, streamSession.TwitchUserId, null, newSegment.Id, category.Name, currentTime, cancellationToken);
                
                return true;
            }
            else if (activeSegment.TwitchCategoryId != category.Id)
            {
                // Category changed - close old segment and create new one
                var oldStreamCategoryId = activeSegment.Id; // Use StreamCategory.Id, not TwitchCategory.Id
                
                await _streamCategoryRepository.CloseSegmentAsync(activeSegment.Id, currentTime, cancellationToken);

                var newSegment = new StreamCategory
                {
                    StreamSessionId = streamSessionId,
                    TwitchCategoryId = category.Id,
                    StartedAt = currentTime,
                    EndedAt = null
                };

                await _streamCategoryRepository.CreateSegmentAsync(newSegment, cancellationToken);
                
                _logger.LogInformation("Category changed for stream {StreamSessionId}: {OldCategory} -> {NewCategory}", 
                    streamSessionId, activeSegment.TwitchCategory?.Name ?? "Unknown", category.Name);

                // Publish event with StreamCategory.Id (not TwitchCategory.Id)
                await PublishCategoryChangedEventAsync(streamSessionId, streamSession.TwitchUserId, oldStreamCategoryId, newSegment.Id, category.Name, currentTime, cancellationToken);
                
                return true;
            }
            else
            {
                // Same category - no action needed
                _logger.LogDebug("Category unchanged for stream {StreamSessionId}, category {CategoryName}", 
                    streamSessionId, category.Name);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing category change for stream {StreamSessionId}", streamSessionId);
            throw;
        }
    }

    private async Task PublishCategoryChangedEventAsync(
        Guid streamSessionId,
        int broadcasterUserId,
        Guid? oldCategoryId, 
        Guid newCategoryId, 
        string categoryName, 
        DateTime changedAt,
        CancellationToken cancellationToken)
    {
        var eventContract = new StreamCategoryChangedEventContract
        {
            MessageId = Guid.NewGuid().ToString(),
            StreamSessionId = streamSessionId,
            BroadcasterUserId = broadcasterUserId,
            OldCategoryId = oldCategoryId,
            NewCategoryId = newCategoryId,
            CategoryName = categoryName,
            ChangedAt = changedAt
        };

        await _publishEndpoint.Publish(eventContract, cancellationToken);
        
        _logger.LogInformation("Published StreamCategoryChangedEvent for stream {StreamSessionId}", streamSessionId);
    }
}

