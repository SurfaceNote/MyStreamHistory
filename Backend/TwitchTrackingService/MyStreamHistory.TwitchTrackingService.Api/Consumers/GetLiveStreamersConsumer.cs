using MassTransit;
using Microsoft.EntityFrameworkCore;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class GetLiveStreamersConsumer(
    TwitchTrackingDbContext dbContext,
    ILogger<GetLiveStreamersConsumer> logger) : IConsumer<GetLiveStreamersRequestContract>
{
    public async Task Consume(ConsumeContext<GetLiveStreamersRequestContract> context)
    {
        try
        {
            var twitchUserIds = await dbContext.StreamSessions
                .AsNoTracking()
                .Where(s => s.IsLive)
                .Select(s => s.TwitchUserId)
                .Distinct()
                .ToListAsync(context.CancellationToken);

            await context.RespondAsync(new GetLiveStreamersResponseContract
            {
                TwitchUserIds = twitchUserIds
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting live streamers");
            await context.RespondAsync(new BaseFailedResponseContract
            {
                Reason = $"Error getting live streamers: {ex.Message}"
            });
        }
    }
}
