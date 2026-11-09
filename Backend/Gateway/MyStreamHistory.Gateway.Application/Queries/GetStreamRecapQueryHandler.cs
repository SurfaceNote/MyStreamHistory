using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Gateway.Application.Services;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Community;
using PlaythroughDto = MyStreamHistory.Shared.Base.Contracts.Playthroughs.PlaythroughDto;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Requests;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Responses;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;
using MyStreamHistory.Shared.Base.Contracts.Viewers;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;
using MyStreamHistory.Shared.Base.Error;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetStreamRecapQueryHandler(ITransportBus bus) : IRequestHandler<GetStreamRecapQuery, StreamRecapDto>
{
    public async Task<StreamRecapDto> Handle(GetStreamRecapQuery request, CancellationToken cancellationToken)
    {
        var streamResponse = await bus.SendRequestAsync<
            GetStreamSessionByIdRequestContract,
            GetStreamSessionByIdResponseContract,
            BaseFailedResponseContract>(
            new GetStreamSessionByIdRequestContract { StreamSessionId = request.StreamSessionId },
            cancellationToken);

        if (streamResponse.IsFailure)
        {
            throw new AppException(streamResponse.Failure!.Reason);
        }

        if (!streamResponse.Success!.Success || streamResponse.Success.StreamSession == null)
        {
            throw new AppException(streamResponse.Success.Error ?? "Stream session not found");
        }

        var session = streamResponse.Success.StreamSession;
        if (!GetStreamRecapQuery.CanRead(request, session.TwitchUserId))
        {
            throw new AppException(ErrorCodes.PermissionDenied, "Only the streamer can view this recap");
        }

        var streamerId = session.TwitchUserId.ToString();

        var audience = await LoadAudienceAsync(session, streamerId, cancellationToken);
        var earlierStreams = await LoadEarlierStreamsAsync(session, cancellationToken);

        var appearances = new Dictionary<string, HashSet<Guid>>();
        var audienceCounts = new Dictionary<Guid, int>();
        if (earlierStreams.Count > 0)
        {
            var comparison = earlierStreams
                .Where(s => !s.IsLive && s.EndedAt.HasValue)
                .OrderByDescending(s => s.StartedAt)
                .Take(StreamRecapCalculator.ComparisonStreams)
                .Select(s => s.Id)
                .ToList();

            var response = await bus.SendRequestAsync<GetViewerAppearancesRequest, GetViewerAppearancesResponse>(
                new GetViewerAppearancesRequest
                {
                    StreamerTwitchUserId = streamerId,
                    ViewerTwitchUserIds = audience?.Select(m => m.TwitchUserId).ToList() ?? [],
                    Sessions = earlierStreams.Select(s => new ViewerAppearanceSession { Id = s.Id, CategoryIds = s.CategoryIds }).ToList(),
                    CountSessionIds = comparison
                },
                cancellationToken);

            if (response.IsSuccess && response.Success?.Success == true)
            {
                appearances = response.Success.Viewers.ToDictionary(v => v.ViewerTwitchUserId, v => v.SessionIds.ToHashSet());
                audienceCounts = response.Success.SessionAudienceCounts.ToDictionary(c => c.SessionId, c => c.Count);
            }
            else
            {
                // Without history every account would look new; show the stream without the comparison.
                audience = null;
            }
        }

        var playthroughs = await LoadPlaythroughsAsync(session.TwitchUserId, cancellationToken);

        return StreamRecapCalculator.Build(
            new RecapStream(session.Id, session.StartedAt, session.EndedAt, session.IsLive),
            session.Audience.Select(point => point.AverageViewers).ToList(),
            audience,
            earlierStreams.Select(s => new RecapStream(s.Id, s.StartedAt, s.EndedAt, s.IsLive)).ToList(),
            appearances,
            audienceCounts,
            playthroughs,
            DateTime.UtcNow);
    }

    private async Task<List<RecapAudienceMember>?> LoadAudienceAsync(
        StreamSessionDetailsDto session,
        string streamerId,
        CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            GetStreamViewersRequestContract,
            GetStreamViewersResponseContract,
            BaseFailedResponseContract>(
            new GetStreamViewersRequestContract
            {
                StreamSessionId = session.Id,
                IncludeSessionPresence = true,
                StreamCategoryIds = session.Categories.Select(c => c.StreamCategoryId).ToList()
            },
            cancellationToken);

        if (!response.IsSuccess || !response.Success!.Success)
        {
            return null;
        }

        return response.Success.Viewers
            .Where(v => v.Viewer != null
                && !RankingExclusions.IsBroadcaster(v.Viewer.TwitchUserId, streamerId)
                && !v.Viewer.IsRankingExcluded)
            .GroupBy(v => v.Viewer!.TwitchUserId)
            .Select(group =>
            {
                var viewer = group.First().Viewer!;
                return new RecapAudienceMember(
                    viewer.TwitchUserId,
                    viewer.DisplayName,
                    viewer.Login,
                    viewer.ProfileImageUrl,
                    group.Max(v => v.MinutesWatched),
                    group.Sum(v => v.ChatPoints));
            })
            .ToList();
    }

    private async Task<List<CommunitySession>> LoadEarlierStreamsAsync(StreamSessionDetailsDto session, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<GetCommunitySessionsRequest, GetCommunitySessionsResponse>(
            new GetCommunitySessionsRequest { TwitchUserId = session.TwitchUserId },
            cancellationToken);

        return response.IsSuccess
            ? response.Success!.Sessions.Where(s => s.Id != session.Id && s.StartedAt < session.StartedAt).ToList()
            : [];
    }

    private async Task<List<PlaythroughDto>> LoadPlaythroughsAsync(int twitchUserId, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            GetPlaythroughSettingsRequestContract,
            GetPlaythroughSettingsResponseContract,
            BaseFailedResponseContract>(
            new GetPlaythroughSettingsRequestContract { TwitchUserId = twitchUserId },
            cancellationToken);

        return response.IsSuccess ? response.Success!.Settings.Playthroughs : [];
    }
}
