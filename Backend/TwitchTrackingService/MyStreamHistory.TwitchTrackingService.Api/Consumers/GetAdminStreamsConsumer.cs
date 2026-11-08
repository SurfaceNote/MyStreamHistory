using MassTransit;
using Microsoft.EntityFrameworkCore;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class GetAdminStreamsConsumer(IStreamSessionRepository repository) : IConsumer<GetAdminStreamsRequest>
{
    public async Task Consume(ConsumeContext<GetAdminStreamsRequest> context)
    {
        var result = await ReadPageAsync(repository, context.Message, context.CancellationToken);
        await context.RespondAsync(result);
    }

    public static async Task<GetAdminStreamsResponse> ReadPageAsync(
        IStreamSessionRepository repository, GetAdminStreamsRequest request, CancellationToken ct = default)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = repository.Query().AsNoTracking();
        var total = await query.CountAsync(ct);
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var page = Math.Clamp(request.Page, 1, lastPage);
        var items = await query.OrderByDescending(s => s.StartedAt).ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new AdminStreamDto
            {
                Id = s.Id, StreamerLogin = s.StreamerLogin, StreamerDisplayName = s.StreamerDisplayName,
                StreamerAvatarUrl = s.StreamerAvatarUrl, StartedAt = s.StartedAt, EndedAt = s.EndedAt, IsLive = s.IsLive,
                Categories = s.StreamCategories.OrderBy(c => c.StartedAt).Select(c => c.TwitchCategory.Name).ToList(),
                CategoryIds = s.StreamCategories.Select(c => c.Id).ToList()
            }).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.Categories = item.Categories.Distinct().ToList();
            var end = item.EndedAt ?? (item.IsLive ? now : (DateTime?)null);
            item.DurationHours = end.HasValue ? Math.Max(0, (end.Value - item.StartedAt).TotalHours) : null;
        }
        return new() { Page = page, PageSize = pageSize, TotalCount = total, Items = items };
    }
}
