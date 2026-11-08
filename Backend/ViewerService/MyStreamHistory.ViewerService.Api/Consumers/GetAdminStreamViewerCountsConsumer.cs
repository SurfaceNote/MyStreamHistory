using MassTransit;
using Microsoft.EntityFrameworkCore;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions;
using MyStreamHistory.ViewerService.Infrastructure.Persistence;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class GetAdminStreamViewerCountsConsumer(ViewerServiceDbContext db) : IConsumer<GetAdminStreamViewerCountsRequest>
{
    public async Task Consume(ConsumeContext<GetAdminStreamViewerCountsRequest> context)
    {
        var result = await ReadCountsAsync(db, context.Message, context.CancellationToken);
        await context.RespondAsync(result);
    }

    public static async Task<GetAdminStreamViewerCountsResponse> ReadCountsAsync(
        ViewerServiceDbContext db, GetAdminStreamViewerCountsRequest request, CancellationToken ct = default)
    {
        if (request.Streams.Count > 100) throw new ArgumentException("At most 100 streams may be requested.");
        var response = new GetAdminStreamViewerCountsResponse();
        foreach (var stream in request.Streams)
        {
            // Union account IDs before counting: one viewer may occur in several categories and presence records.
            var categoryViewers = db.ViewerCategoryWatches.AsNoTracking()
                .Where(w => stream.CategoryIds.Contains(w.StreamCategoryId)).Select(w => w.Viewer.TwitchUserId);
            var presenceViewers = db.CommunityPresences.AsNoTracking()
                .Where(p => p.StreamSessionId == stream.StreamSessionId).Select(p => p.ViewerTwitchUserId);
            response.Counts[stream.StreamSessionId] = await categoryViewers.Union(presenceViewers).CountAsync(ct);
        }
        return response;
    }
}
