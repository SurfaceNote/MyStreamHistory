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
    private static readonly TimeSpan ReconciliationGracePeriod = TimeSpan.FromMinutes(10);

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

        TwitchStreamDto? twitchStream = null;
        try
        {
            twitchStream = (await _twitchApiClient.GetStreamsAsync(
                    new List<int> { eventDto.BroadcasterUserId },
                    cancellationToken))
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not enrich stream.online for {BroadcasterUserLogin}; polling will fill stream metadata later",
                eventDto.BroadcasterUserLogin);
        }

        var activeSession = await _repository.GetActiveByTwitchUserIdAsync(
            eventDto.BroadcasterUserId,
            cancellationToken);
        if (activeSession != null
            && !StreamReconciliationPlanner.IsDifferentBroadcast(activeSession, twitchStream, eventDto.StartedAt))
        {
            _logger.LogInformation(
                "Ignoring duplicate stream.online for active session {StreamSessionId}",
                activeSession.Id);
            return;
        }

        var userProfile = await _userProfileService.GetUserProfileAsync(eventDto.BroadcasterUserId, cancellationToken);
        var streamSession = CreateSession(
            eventDto.BroadcasterUserId,
            twitchStream?.UserLogin ?? eventDto.BroadcasterUserLogin,
            userProfile?.DisplayName ?? eventDto.BroadcasterUserName,
            userProfile?.Avatar,
            twitchStream?.StartedAt ?? eventDto.StartedAt,
            twitchStream,
            DateTime.UtcNow);

        {
            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                activeSession = await _repository.GetActiveByTwitchUserIdAsync(
                    eventDto.BroadcasterUserId,
                    cancellationToken);

                if (activeSession != null)
                {
                    if (!StreamReconciliationPlanner.IsDifferentBroadcast(activeSession, twitchStream, eventDto.StartedAt))
                    {
                        await transaction.CommitAsync(cancellationToken);
                        return;
                    }

                    var restartEndTime = ResolveRestartEndTime(activeSession, streamSession.StartedAt);
                    await EndStreamSessionCoreAsync(
                        activeSession,
                        restartEndTime,
                        activeSession.StreamerLogin,
                        activeSession.StreamerDisplayName,
                        "stream.online restart",
                        cancellationToken);
                }

                await _repository.AddAsync(streamSession, cancellationToken);
                await PublishStreamCreatedAsync(
                    streamSession,
                    eventDto.Type,
                    cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        _logger.LogInformation(
            "Stream session created for broadcaster {BroadcasterUserLogin} with StreamSessionId {StreamSessionId}",
            eventDto.BroadcasterUserLogin,
            streamSession.Id);

        try
        {
            if (twitchStream != null && !string.IsNullOrWhiteSpace(twitchStream.GameId))
            {
                await _categoryTrackingService.ProcessSingleStreamCategoryAsync(
                    streamSession.Id,
                    twitchStream.GameId,
                    twitchStream.GameName,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error creating initial category for stream {StreamSessionId}; polling will retry",
                streamSession.Id);
        }
    }

    public async Task HandleStreamOfflineAsync(StreamOfflineEventDto eventDto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stream offline event received for broadcaster {BroadcasterUserLogin}", eventDto.BroadcasterUserLogin);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var activeSession = await _repository.GetActiveByTwitchUserIdAsync(
                eventDto.BroadcasterUserId,
                cancellationToken);

            if (activeSession == null)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            if (eventDto.OccurredAt.HasValue && eventDto.OccurredAt.Value < activeSession.StartedAt)
            {
                _logger.LogWarning(
                    "Ignoring stale stream.offline at {OccurredAt} for newer session {StreamSessionId} started at {StartedAt}",
                    eventDto.OccurredAt,
                    activeSession.Id,
                    activeSession.StartedAt);
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var endTime = eventDto.OccurredAt ?? DateTime.UtcNow;
            if (endTime < activeSession.StartedAt)
            {
                endTime = activeSession.StartedAt;
            }

            await EndStreamSessionCoreAsync(
                activeSession,
                endTime,
                eventDto.BroadcasterUserLogin,
                eventDto.BroadcasterUserName,
                "EventSub",
                cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task ReconcileActiveStreamsAsync(
        IReadOnlyCollection<StreamSession> activeSessions,
        IReadOnlyCollection<TwitchStreamDto> streams,
        IReadOnlyCollection<TrackedUserProfileDto> trackedUsers,
        DateTime checkedAt,
        CancellationToken cancellationToken = default)
    {
        var plan = StreamReconciliationPlanner.Build(
            activeSessions,
            streams,
            trackedUsers.Select(user => user.TwitchUserId).ToArray());
        var streamsByUserId = plan.StreamsByUserId;
        var sessionsByUserId = plan.SessionsByUserId;
        var profilesByUserId = trackedUsers
            .GroupBy(user => user.TwitchUserId)
            .ToDictionary(group => group.Key, group => group.First());
        var restartedUserIds = plan.RestartedUserIds;
        var confirmedUserIds = plan.ConfirmedUserIds;

        var createdSessions = new List<StreamSession>();
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var userId in restartedUserIds)
            {
                var oldSession = sessionsByUserId[userId];
                var stream = streamsByUserId[userId];
                var endTime = ResolveRestartEndTime(oldSession, stream.StartedAt);
                await EndStreamSessionCoreAsync(
                    oldSession,
                    endTime,
                    oldSession.StreamerLogin,
                    oldSession.StreamerDisplayName,
                    "polling restart reconciliation",
                    cancellationToken);

                var newSession = CreateSessionFromTwitchStream(
                    userId,
                    stream,
                    profilesByUserId.GetValueOrDefault(userId),
                    checkedAt);
                await _repository.AddAsync(newSession, cancellationToken);
                await PublishStreamCreatedAsync(newSession, stream.Type, cancellationToken);
                createdSessions.Add(newSession);
            }

            await _repository.ConfirmActiveAsync(
                confirmedUserIds.Select(userId => sessionsByUserId[userId].Id).ToArray(),
                checkedAt,
                cancellationToken);

            foreach (var userId in confirmedUserIds)
            {
                var stream = streamsByUserId[userId];
                var session = sessionsByUserId[userId];
                await _repository.UpdatePollingDataAsync(
                    session.Id,
                    stream.Id,
                    stream.Title,
                    stream.GameName,
                    stream.ViewerCount,
                    cancellationToken);
                await _publishEndpoint.Publish(new StreamLiveConfirmationRestoredEventContract
                {
                    StreamSessionId = session.Id,
                    BroadcasterUserId = userId,
                    ConfirmedAt = checkedAt
                }, cancellationToken);
            }

            var missingSessions = plan.MissingSessions;

            await _repository.MarkMissingAsync(
                missingSessions.Select(session => session.Id).ToArray(),
                checkedAt,
                cancellationToken);

            foreach (var session in missingSessions)
            {
                var missingSince = session.MissingSinceAt ?? checkedAt;
                await _publishEndpoint.Publish(new StreamLiveConfirmationLostEventContract
                {
                    StreamSessionId = session.Id,
                    BroadcasterUserId = session.TwitchUserId,
                    MissingSince = missingSince
                }, cancellationToken);

                if (missingSince <= checkedAt - ReconciliationGracePeriod)
                {
                    var endTime = session.LastConfirmedLiveAt ?? missingSince;
                    await EndStreamSessionCoreAsync(
                        session,
                        endTime,
                        session.StreamerLogin,
                        session.StreamerDisplayName,
                        "polling reconciliation",
                        cancellationToken);
                }
            }

            foreach (var userId in plan.DiscoveredUserIds)
            {
                var stream = streamsByUserId[userId];
                var profile = profilesByUserId[userId];
                var newSession = CreateSessionFromTwitchStream(userId, stream, profile, checkedAt);
                await _repository.AddAsync(newSession, cancellationToken);
                await PublishStreamCreatedAsync(newSession, stream.Type, cancellationToken);
                createdSessions.Add(newSession);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        _logger.LogInformation(
            "Reconciled streams: {ActiveCount} local, {ConfirmedCount} confirmed, {RestartedCount} restarted, {CreatedCount} discovered",
            activeSessions.Count,
            confirmedUserIds.Count,
            restartedUserIds.Count,
            createdSessions.Count - restartedUserIds.Count);
    }

    private async Task<bool> EndStreamSessionCoreAsync(
        StreamSession activeSession,
        DateTime endTime,
        string broadcasterUserLogin,
        string broadcasterUserName,
        string source,
        CancellationToken cancellationToken)
    {
        if (!await _repository.TryEndActiveAsync(activeSession.Id, endTime, cancellationToken))
        {
            _logger.LogDebug(
                "Stream session {StreamSessionId} was already ended while processing {Source}",
                activeSession.Id,
                source);
            return false;
        }

        var activeSegment = await _streamCategoryRepository.GetActiveSegmentByStreamIdAsync(
            activeSession.Id,
            cancellationToken);

        if (activeSegment != null)
        {
            var segmentEndTime = endTime < activeSegment.StartedAt
                ? activeSegment.StartedAt
                : endTime;
            await _streamCategoryRepository.CloseSegmentAsync(
                activeSegment.Id,
                segmentEndTime,
                cancellationToken);
            _logger.LogInformation("Closed active category segment for stream {StreamSessionId}", activeSession.Id);
        }

        await _publishEndpoint.Publish(new StreamEndedEventContract
        {
            StreamSessionId = activeSession.Id,
            BroadcasterUserId = activeSession.TwitchUserId,
            BroadcasterUserLogin = broadcasterUserLogin,
            BroadcasterUserName = broadcasterUserName,
            EndedAt = endTime
        }, cancellationToken);

        _logger.LogInformation(
            "Stream session {StreamSessionId} ended from {Source} at {EndedAt}",
            activeSession.Id,
            source,
            endTime);

        return true;
    }

    private async Task PublishStreamCreatedAsync(
        StreamSession streamSession,
        string streamType,
        CancellationToken cancellationToken)
    {
        await _publishEndpoint.Publish(new StreamCreatedEventContract
        {
            StreamSessionId = streamSession.Id,
            BroadcasterUserId = streamSession.TwitchUserId,
            BroadcasterUserLogin = streamSession.StreamerLogin,
            BroadcasterUserName = streamSession.StreamerDisplayName,
            StartedAt = streamSession.StartedAt,
            Type = streamType
        }, cancellationToken);
    }

    private static DateTime ResolveRestartEndTime(StreamSession activeSession, DateTime nextStartedAt)
    {
        var endTime = activeSession.LastConfirmedLiveAt ?? nextStartedAt;
        if (endTime > nextStartedAt)
        {
            endTime = nextStartedAt;
        }

        return endTime < activeSession.StartedAt
            ? activeSession.StartedAt
            : endTime;
    }

    private static StreamSession CreateSessionFromTwitchStream(
        int twitchUserId,
        TwitchStreamDto stream,
        TrackedUserProfileDto? profile,
        DateTime confirmedAt)
    {
        return CreateSession(
            twitchUserId,
            stream.UserLogin,
            string.IsNullOrWhiteSpace(profile?.DisplayName) ? stream.UserName : profile.DisplayName,
            profile?.AvatarUrl,
            stream.StartedAt,
            stream,
            confirmedAt);
    }

    private static StreamSession CreateSession(
        int twitchUserId,
        string streamerLogin,
        string streamerDisplayName,
        string? streamerAvatarUrl,
        DateTime startedAt,
        TwitchStreamDto? stream,
        DateTime confirmedAt)
    {
        return new StreamSession
        {
            TwitchUserId = twitchUserId,
            StreamId = stream?.Id,
            StreamerLogin = streamerLogin,
            StreamerDisplayName = streamerDisplayName,
            StreamerAvatarUrl = streamerAvatarUrl,
            StartedAt = startedAt,
            IsLive = true,
            LastConfirmedLiveAt = confirmedAt,
            StreamTitle = stream?.Title,
            GameName = stream?.GameName,
            ViewerCount = stream?.ViewerCount
        };
    }

    public async Task<List<StreamSessionDto>> GetRecentStreamsByTwitchUserIdAsync(int twitchUserId, int count = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting recent {Count} streams for TwitchUserId {TwitchUserId}", count, twitchUserId);

        var sessionDtos = await _repository.GetRecentStreamsByTwitchUserIdAsync(
            twitchUserId,
            count,
            cancellationToken);

        _logger.LogInformation("Found {SessionCount} recent streams for TwitchUserId {TwitchUserId}", sessionDtos.Count, twitchUserId);

        return sessionDtos;
    }

    public async Task<StreamSessionDto?> GetStreamSessionByIdAsync(Guid streamSessionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting stream session details for StreamSessionId {StreamSessionId}", streamSessionId);

        var session = await _repository.GetDtoByIdAsync(streamSessionId, cancellationToken);

        if (session == null)
        {
            _logger.LogWarning("Stream session not found for StreamSessionId {StreamSessionId}", streamSessionId);
            return null;
        }

        _logger.LogInformation("Found stream session for StreamSessionId {StreamSessionId}", streamSessionId);

        return session;
    }
}

