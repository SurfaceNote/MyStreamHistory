using MassTransit;
using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Application.UnitOfWork;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Services;

public class StreamSessionService : IStreamSessionService
{
    private readonly IStreamSessionRepository _repository;
    private readonly IStreamCategoryRepository _streamCategoryRepository;
    private readonly ICategoryTrackingService _categoryTrackingService;
    private readonly ITwitchApiClient _twitchApiClient;
    private readonly IUserProfileService _userProfileService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<StreamSessionService> _logger;

    public StreamSessionService(
        IStreamSessionRepository repository, 
        IStreamCategoryRepository streamCategoryRepository,
        ICategoryTrackingService categoryTrackingService,
        ITwitchApiClient twitchApiClient,
        IUserProfileService userProfileService,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint,
        ILogger<StreamSessionService> logger)
    {
        _repository = repository;
        _streamCategoryRepository = streamCategoryRepository;
        _categoryTrackingService = categoryTrackingService;
        _twitchApiClient = twitchApiClient;
        _userProfileService = userProfileService;
        _unitOfWork = unitOfWork;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task HandleStreamOnlineAsync(StreamOnlineEventDto eventDto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stream online event received for broadcaster {BroadcasterUserLogin}", eventDto.BroadcasterUserLogin);

        // Check if there's already an active stream session for this user
        var existingSessions = await _repository.GetAllAsync(cancellationToken);
        var activeSession = existingSessions.FirstOrDefault(s => s.TwitchUserId == eventDto.BroadcasterUserId && s.IsLive);

        if (activeSession != null)
        {
            _logger.LogWarning("Active stream session already exists for broadcaster {BroadcasterUserLogin}", eventDto.BroadcasterUserLogin);
            return;
        }

        // Get user profile from AuthService
        var userProfile = await _userProfileService.GetUserProfileAsync(eventDto.BroadcasterUserId, cancellationToken);
        
        var streamSession = new StreamSession
        {
            TwitchUserId = eventDto.BroadcasterUserId,
            StreamerLogin = eventDto.BroadcasterUserLogin,
            StreamerDisplayName = userProfile?.DisplayName ?? eventDto.BroadcasterUserName,
            StreamerAvatarUrl = userProfile?.Avatar,
            StartedAt = eventDto.StartedAt,
            IsLive = true
        };

        await _repository.AddAsync(streamSession, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stream session created for broadcaster {BroadcasterUserLogin} with StreamSessionId {StreamSessionId}", 
            eventDto.BroadcasterUserLogin, streamSession.Id);

        // Get stream information from Twitch API to create initial category
        try
        {
            var streams = await _twitchApiClient.GetStreamsAsync(new List<int> { eventDto.BroadcasterUserId }, cancellationToken);
            
            if (streams.Count > 0 && !string.IsNullOrEmpty(streams[0].GameId))
            {
                var stream = streams[0];
                _logger.LogInformation("Creating initial category for stream {StreamSessionId}: {GameName} (ID: {GameId})", 
                    streamSession.Id, stream.GameName, stream.GameId);

                // Create initial category
                await _categoryTrackingService.ProcessSingleStreamCategoryAsync(
                    streamSession.Id, 
                    stream.GameId, 
                    stream.GameName, 
                    cancellationToken);
            }
            else
            {
                _logger.LogWarning("No category information available for stream {StreamSessionId}", streamSession.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching initial category for stream {StreamSessionId}, will be created later", streamSession.Id);
            // Don't throw exception - category will be created on next polling or channel.update
        }

        // Publish event for other microservices
        var streamCreatedEvent = new StreamCreatedEventContract
        {
            StreamSessionId = streamSession.Id,
            BroadcasterUserId = eventDto.BroadcasterUserId,
            BroadcasterUserLogin = eventDto.BroadcasterUserLogin,
            BroadcasterUserName = eventDto.BroadcasterUserName,
            StartedAt = eventDto.StartedAt,
            Type = eventDto.Type
        };

        await _publishEndpoint.Publish(streamCreatedEvent, cancellationToken);
        
        _logger.LogInformation("Published StreamCreatedEvent for broadcaster {BroadcasterUserLogin}", eventDto.BroadcasterUserLogin);
    }

    public async Task HandleStreamOfflineAsync(StreamOfflineEventDto eventDto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stream offline event received for broadcaster {BroadcasterUserLogin}", eventDto.BroadcasterUserLogin);

        var allSessions = await _repository.GetAllAsync(cancellationToken);
        var activeSession = allSessions.FirstOrDefault(s => s.TwitchUserId == eventDto.BroadcasterUserId && s.IsLive);

        if (activeSession == null)
        {
            _logger.LogWarning("No active stream session found for broadcaster {BroadcasterUserLogin}", eventDto.BroadcasterUserLogin);
            return;
        }

        var endTime = DateTime.UtcNow;
        activeSession.IsLive = false;
        activeSession.EndedAt = endTime;

        await _repository.UpdateAsync(activeSession, cancellationToken);
        
        // Close active category segment if exists
        var activeSegment = await _streamCategoryRepository.GetActiveSegmentByStreamIdAsync(activeSession.Id, cancellationToken);
        if (activeSegment != null)
        {
            await _streamCategoryRepository.CloseSegmentAsync(activeSegment.Id, endTime, cancellationToken);
            _logger.LogInformation("Closed active category segment for stream {StreamSessionId}", activeSession.Id);
        }
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stream session ended for broadcaster {BroadcasterUserLogin} with StreamSessionId {StreamSessionId}", 
            eventDto.BroadcasterUserLogin, activeSession.Id);

        // Publish event for other microservices
        var streamEndedEvent = new StreamEndedEventContract
        {
            StreamSessionId = activeSession.Id,
            BroadcasterUserId = eventDto.BroadcasterUserId,
            BroadcasterUserLogin = eventDto.BroadcasterUserLogin,
            BroadcasterUserName = eventDto.BroadcasterUserName,
            EndedAt = endTime
        };

        await _publishEndpoint.Publish(streamEndedEvent, cancellationToken);
        
        _logger.LogInformation("Published StreamEndedEvent for broadcaster {BroadcasterUserLogin}", eventDto.BroadcasterUserLogin);
    }

    public async Task UpdateActiveStreamsDataAsync(List<TwitchStreamDto> streams, CancellationToken cancellationToken = default)
    {
        if (streams == null || streams.Count == 0)
        {
            _logger.LogDebug("No active streams to update");
            return;
        }

        _logger.LogInformation("Updating data for {StreamCount} active streams", streams.Count);

        var allSessions = await _repository.GetAllAsync(cancellationToken);
        var activeSessions = allSessions.Where(s => s.IsLive).ToList();

        var updatedCount = 0;

        foreach (var stream in streams)
        {
            if (!int.TryParse(stream.UserId, out var userId))
            {
                _logger.LogWarning("Invalid user ID: {UserId}", stream.UserId);
                continue;
            }

            var session = activeSessions.FirstOrDefault(s => s.TwitchUserId == userId);
            
            if (session == null)
            {
                _logger.LogWarning("No active session found for broadcaster {UserLogin} (TwitchId: {UserId})", 
                    stream.UserLogin, userId);
                continue;
            }

            // Update stream data
            var hasChanges = false;

            if (session.StreamId != stream.Id)
            {
                session.StreamId = stream.Id;
                hasChanges = true;
            }

            if (session.StreamTitle != stream.Title)
            {
                session.StreamTitle = stream.Title;
                hasChanges = true;
            }

            if (session.GameName != stream.GameName)
            {
                session.GameName = stream.GameName;
                hasChanges = true;
            }

            if (session.ViewerCount != stream.ViewerCount)
            {
                session.ViewerCount = stream.ViewerCount;
                hasChanges = true;
            }

            if (hasChanges)
            {
                await _repository.UpdateAsync(session, cancellationToken);
                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Updated {UpdatedCount} stream sessions with fresh data", updatedCount);
        }
        else
        {
            _logger.LogDebug("No changes detected in stream data");
        }
    }

    public async Task<List<StreamSessionDto>> GetRecentStreamsByTwitchUserIdAsync(int twitchUserId, int count = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting recent {Count} streams for TwitchUserId {TwitchUserId}", count, twitchUserId);

        var sessions = await _repository.GetRecentStreamsByTwitchUserIdAsync(twitchUserId, count, cancellationToken);

        var sessionDtos = sessions.Select(s => new StreamSessionDto
        {
            Id = s.Id,
            StreamId = s.StreamId,
            TwitchUserId = s.TwitchUserId,
            StreamerLogin = s.StreamerLogin,
            StreamerDisplayName = s.StreamerDisplayName,
            StartedAt = s.StartedAt,
            EndedAt = s.EndedAt,
            IsLive = s.IsLive,
            StreamTitle = s.StreamTitle,
            GameName = s.GameName,
            ViewerCount = s.ViewerCount,
            Categories = s.StreamCategories
                .Select(sc => sc.TwitchCategory)
                .Distinct()
                .Select(c => new TwitchCategoryDto
                {
                    TwitchId = c.TwitchId,
                    Name = c.Name,
                    BoxArtUrl = c.BoxArtUrl,
                    IgdbId = c.IgdbId
                })
                .ToList()
        }).ToList();

        _logger.LogInformation("Found {SessionCount} recent streams for TwitchUserId {TwitchUserId}", sessionDtos.Count, twitchUserId);

        return sessionDtos;
    }

    public async Task<StreamSessionDto?> GetStreamSessionByIdAsync(Guid streamSessionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting stream session details for StreamSessionId {StreamSessionId}", streamSessionId);

        var session = await _repository.GetByIdAsync(streamSessionId, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Stream session not found for StreamSessionId {StreamSessionId}", streamSessionId);
            return null;
        }

        var sessionDto = new StreamSessionDto
        {
            Id = session.Id,
            StreamId = session.StreamId,
            TwitchUserId = session.TwitchUserId,
            StreamerLogin = session.StreamerLogin,
            StreamerDisplayName = session.StreamerDisplayName,
            StreamerAvatarUrl = session.StreamerAvatarUrl,
            StartedAt = session.StartedAt,
            EndedAt = session.EndedAt,
            IsLive = session.IsLive,
            StreamTitle = session.StreamTitle,
            GameName = session.GameName,
            ViewerCount = session.ViewerCount,
            Categories = session.StreamCategories
                .Select(sc => sc.TwitchCategory)
                .Distinct()
                .Select(c => new TwitchCategoryDto
                {
                    TwitchId = c.TwitchId,
                    Name = c.Name,
                    BoxArtUrl = c.BoxArtUrl,
                    IgdbId = c.IgdbId
                })
                .ToList()
        };

        _logger.LogInformation("Found stream session for StreamSessionId {StreamSessionId}", streamSessionId);

        return sessionDto;
    }
}

