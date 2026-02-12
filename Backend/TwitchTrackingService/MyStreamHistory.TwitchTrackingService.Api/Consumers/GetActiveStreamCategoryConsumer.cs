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
                .Where(s => s.TwitchUserId == twitchUserId && s.IsLive)
                .OrderByDescending(s => s.StartedAt)
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

            // Get current active category (most recent without EndedAt)
            var currentCategory = await _context.StreamCategories
                .Where(sc => sc.StreamSessionId == streamSession.Id && sc.EndedAt == null)
                .OrderByDescending(sc => sc.StartedAt)
                .Include(sc => sc.TwitchCategory)
                .FirstOrDefaultAsync(context.CancellationToken);

            await context.RespondAsync(new GetActiveStreamCategoryResponseContract
            {
                Success = true,
                StreamSessionId = streamSession.Id,
                StreamCategoryId = currentCategory?.Id,
                CategoryName = currentCategory?.TwitchCategory?.Name
            });

            _logger.LogDebug("Found active stream category for TwitchUserId: {TwitchUserId}, StreamSessionId: {StreamSessionId}, CategoryId: {CategoryId}",
                twitchUserId, streamSession.Id, currentCategory?.Id);
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

