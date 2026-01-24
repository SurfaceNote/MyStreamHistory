using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Application.UnitOfWork;
using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Services;

public class StreamSessionService : IStreamSessionService
{
    private readonly IStreamSessionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StreamSessionService> _logger;

    public StreamSessionService(IStreamSessionRepository repository, IUnitOfWork unitOfWork, ILogger<StreamSessionService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
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

        var streamSession = new StreamSession
        {
            TwitchUserId = eventDto.BroadcasterUserId,
            StreamerLogin = eventDto.BroadcasterUserLogin,
            StreamerDisplayName = eventDto.BroadcasterUserName,
            StartedAt = eventDto.StartedAt,
            IsLive = true
        };

        await _repository.AddAsync(streamSession, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stream session created for broadcaster {BroadcasterUserLogin}", eventDto.BroadcasterUserLogin);
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

        activeSession.IsLive = false;
        activeSession.EndedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(activeSession, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stream session ended for broadcaster {BroadcasterUserLogin}", eventDto.BroadcasterUserLogin);
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
            TwitchUserId = s.TwitchUserId,
            StreamerLogin = s.StreamerLogin,
            StreamerDisplayName = s.StreamerDisplayName,
            StartedAt = s.StartedAt,
            EndedAt = s.EndedAt,
            IsLive = s.IsLive,
            StreamTitle = s.StreamTitle,
            GameName = s.GameName,
            ViewerCount = s.ViewerCount
        }).ToList();

        _logger.LogInformation("Found {SessionCount} recent streams for TwitchUserId {TwitchUserId}", sessionDtos.Count, twitchUserId);

        return sessionDtos;
    }
}

