using MassTransit;
using Microsoft.EntityFrameworkCore;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class GetActiveStreamCategoryConsumer : IConsumer<GetActiveStreamCategoryRequestContract>
{
    private readonly TwitchTrackingDbContext _context;
    private readonly ILogger<GetActiveStreamCategoryConsumer> _logger;

    public GetActiveStreamCategoryConsumer(
        TwitchTrackingDbContext context,
        ILogger<GetActiveStreamCategoryConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetActiveStreamCategoryRequestContract> context)
    {
        var request = context.Message;
        
        _logger.LogDebug("Getting active stream category for TwitchUserId: {TwitchUserId}", request.TwitchUserId);

        try
        {
            // Parse TwitchUserId to int
            if (!int.TryParse(request.TwitchUserId, out var twitchUserId))
            {
                await context.RespondAsync(new GetActiveStreamCategoryResponseContract
                {
                    Success = false,
                    Error = $"Invalid TwitchUserId format: {request.TwitchUserId}"
                });
                return;
            }

            // Find active stream session
            var streamSession = await _context.StreamSessions
                .AsNoTracking()
                .Where(s => s.TwitchUserId == twitchUserId && s.IsLive)
                .OrderByDescending(s => s.StartedAt)
                .Select(s => new GetActiveStreamCategoryResponseContract
                {
                    Success = true,
                    StreamSessionId = s.Id,
                    StreamCategoryId = s.StreamCategories
                        .Where(sc => sc.EndedAt == null)
                        .OrderByDescending(sc => sc.StartedAt)
                        .Select(sc => (Guid?)sc.Id)
                        .FirstOrDefault(),
                    CategoryName = s.StreamCategories
                        .Where(sc => sc.EndedAt == null)
                        .OrderByDescending(sc => sc.StartedAt)
                        .Select(sc => sc.TwitchCategory.Name)
                        .FirstOrDefault(),
                    IsLiveConfirmed = s.MissingSinceAt == null
                })
                .FirstOrDefaultAsync(context.CancellationToken);

            if (streamSession == null)
            {
                await context.RespondAsync(new GetActiveStreamCategoryResponseContract
                {
                    Success = false,
                    Error = $"No active stream found for TwitchUserId: {twitchUserId}"
                });
                return;
            }

            await context.RespondAsync(streamSession);

            _logger.LogDebug("Found active stream category for TwitchUserId: {TwitchUserId}, StreamSessionId: {StreamSessionId}, CategoryId: {CategoryId}",
                twitchUserId, streamSession.StreamSessionId, streamSession.StreamCategoryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active stream category for TwitchUserId: {TwitchUserId}", request.TwitchUserId);
            
            await context.RespondAsync(new GetActiveStreamCategoryResponseContract
            {
                Success = false,
                Error = $"Internal error: {ex.Message}"
            });
        }
    }
}

