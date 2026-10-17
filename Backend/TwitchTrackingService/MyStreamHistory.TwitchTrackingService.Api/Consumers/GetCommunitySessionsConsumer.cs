using MassTransit;
using Microsoft.EntityFrameworkCore;
using MyStreamHistory.Shared.Base.Contracts.Community;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class GetCommunitySessionsConsumer(IStreamSessionRepository repository) : IConsumer<GetCommunitySessionsRequest>
{
    public async Task Consume(ConsumeContext<GetCommunitySessionsRequest> context)
    {
        var sessions = await repository.Query().AsNoTracking().Where(s => s.TwitchUserId == context.Message.TwitchUserId)
            .Select(s => new CommunitySession
            {
                Id = s.Id, StartedAt = s.StartedAt, EndedAt = s.EndedAt, IsLive = s.IsLive,
                CategoryIds = s.StreamCategories.Select(c => c.Id).ToList()
            }).ToListAsync(context.CancellationToken);
        await context.RespondAsync(new GetCommunitySessionsResponse { Sessions = sessions });
    }
}
