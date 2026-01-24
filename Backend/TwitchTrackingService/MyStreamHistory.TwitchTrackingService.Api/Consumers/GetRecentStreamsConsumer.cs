using MassTransit;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class GetRecentStreamsConsumer : IConsumer<GetRecentStreamsRequestContract>
{
    private readonly IStreamSessionService _streamSessionService;
    private readonly ILogger<GetRecentStreamsConsumer> _logger;

    public GetRecentStreamsConsumer(IStreamSessionService streamSessionService, ILogger<GetRecentStreamsConsumer> logger)
    {
        _streamSessionService = streamSessionService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetRecentStreamsRequestContract> context)
    {
        _logger.LogInformation("Received request to get recent streams for TwitchUserId {TwitchUserId}", 
            context.Message.TwitchUserId);

        try
        {
            var streams = await _streamSessionService.GetRecentStreamsByTwitchUserIdAsync(
                context.Message.TwitchUserId, 
                context.Message.Count, 
                context.CancellationToken);

            var response = new GetRecentStreamsResponseContract
            {
                StreamSessions = streams.Select(s => new Shared.Base.Contracts.StreamSessions.StreamSessionDto
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
                }).ToList()
            };

            await context.RespondAsync(response);
            _logger.LogInformation("Successfully responded with {Count} stream sessions", response.StreamSessions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting recent streams for TwitchUserId {TwitchUserId}", 
                context.Message.TwitchUserId);
            
            await context.RespondAsync(new BaseFailedResponseContract
            {
                Reason = $"Error getting recent streams: {ex.Message}"
            });
        }
    }
}

