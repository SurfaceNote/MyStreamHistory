using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetStreamDetailsByIdQueryHandler : IRequestHandler<GetStreamDetailsByIdQuery, StreamDetailsDto>
{
    private readonly ITransportBus _bus;

    public GetStreamDetailsByIdQueryHandler(ITransportBus bus)
    {
        _bus = bus;
    }

    public async Task<StreamDetailsDto> Handle(GetStreamDetailsByIdQuery request, CancellationToken cancellationToken)
    {
        // Get stream session details from TwitchTrackingService
        var streamResponse = await _bus.SendRequestAsync<
            GetStreamSessionByIdRequestContract, 
            GetStreamSessionByIdResponseContract, 
            BaseFailedResponseContract>
        (
            new GetStreamSessionByIdRequestContract 
            { 
                StreamSessionId = request.StreamSessionId 
            }, 
            cancellationToken
        );

        if (streamResponse.IsFailure)
        {
            throw new AppException(streamResponse.Failure!.Reason);
        }

        if (!streamResponse.Success!.Success || streamResponse.Success.StreamSession == null)
        {
            throw new AppException(streamResponse.Success.Error ?? "Stream session not found");
        }

        var streamSession = streamResponse.Success.StreamSession;

        // Get viewers from ViewerService
        var categoryIds = streamSession.Categories.Select(c => c.StreamCategoryId).ToList();
        
        var viewersResponse = await _bus.SendRequestAsync<
            GetStreamViewersRequestContract, 
            GetStreamViewersResponseContract, 
            BaseFailedResponseContract>
        (
            new GetStreamViewersRequestContract 
            { 
                StreamSessionId = request.StreamSessionId,
                StreamCategoryIds = categoryIds
            }, 
            cancellationToken
        );

        var viewers = new List<StreamViewerDto>();
        
        if (viewersResponse.IsSuccess && viewersResponse.Success!.Success)
        {
            viewers = viewersResponse.Success.Viewers.Select(v => new StreamViewerDto
            {
                TwitchUserId = v.Viewer?.TwitchUserId ?? string.Empty,
                DisplayName = v.Viewer?.DisplayName ?? string.Empty,
                Login = v.Viewer?.Login ?? string.Empty,
                ProfileImageUrl = v.Viewer?.ProfileImageUrl,
                MinutesWatched = v.MinutesWatched,
                ChatPoints = v.ChatPoints,
                ViewerPoints = v.MinutesWatched // 1 minute = 1 viewer point for simplicity
            }).ToList();
        }

        // Calculate unique viewers per category
        // For this, we need to get viewer watches per category (not aggregated)
        var categoryViewerCounts = new Dictionary<Guid, int>();
        
        // We'll need to make individual requests per category to get unique viewer counts
        foreach (var category in streamSession.Categories)
        {
            var categoryViewersResponse = await _bus.SendRequestAsync<
                GetStreamViewersRequestContract, 
                GetStreamViewersResponseContract, 
                BaseFailedResponseContract>
            (
                new GetStreamViewersRequestContract 
                { 
                    StreamSessionId = request.StreamSessionId,
                    StreamCategoryIds = new List<Guid> { category.StreamCategoryId }
                }, 
                cancellationToken
            );

            if (categoryViewersResponse.IsSuccess && categoryViewersResponse.Success!.Success)
            {
                categoryViewerCounts[category.StreamCategoryId] = categoryViewersResponse.Success.Viewers
                    .Select(v => v.ViewerId)
                    .Distinct()
                    .Count();
            }
            else
            {
                categoryViewerCounts[category.StreamCategoryId] = 0;
            }
        }

        // Build the response
        var result = new StreamDetailsDto
        {
            Id = streamSession.Id,
            StreamId = streamSession.StreamId,
            TwitchUserId = streamSession.TwitchUserId,
            StreamerLogin = streamSession.StreamerLogin,
            StreamerDisplayName = streamSession.StreamerDisplayName,
            StreamerAvatarUrl = streamSession.StreamerAvatarUrl,
            StartedAt = streamSession.StartedAt,
            EndedAt = streamSession.EndedAt,
            IsLive = streamSession.IsLive,
            StreamTitle = streamSession.StreamTitle,
            GameName = streamSession.GameName,
            ViewerCount = streamSession.ViewerCount,
            Categories = streamSession.Categories.Select(c => new CategoryDetailsDto
            {
                StreamCategoryId = c.StreamCategoryId,
                TwitchId = c.TwitchCategoryId,
                Name = c.Name,
                BoxArtUrl = c.BoxArtUrl,
                StartedAt = c.StartedAt,
                EndedAt = c.EndedAt,
                DurationMinutes = c.DurationMinutes,
                UniqueViewers = categoryViewerCounts.GetValueOrDefault(c.StreamCategoryId, 0)
            }).ToList(),
            Viewers = viewers
        };

        return result;
    }
}

