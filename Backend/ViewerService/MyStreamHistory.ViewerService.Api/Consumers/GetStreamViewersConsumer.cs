using MassTransit;
using Microsoft.EntityFrameworkCore;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Viewers;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;
using MyStreamHistory.Shared.Base.Contracts.Viewers.Responses;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Infrastructure.Persistence;

namespace MyStreamHistory.ViewerService.Api.Consumers;

public class GetStreamViewersConsumer : IConsumer<GetStreamViewersRequestContract>
{
    private readonly IViewerCategoryWatchRepository _categoryWatchRepository;
    private readonly ILogger<GetStreamViewersConsumer> _logger;
    private readonly ViewerServiceDbContext _db;

    public GetStreamViewersConsumer(
        IViewerCategoryWatchRepository categoryWatchRepository,
        ILogger<GetStreamViewersConsumer> logger,
        ViewerServiceDbContext db)
    {
        _categoryWatchRepository = categoryWatchRepository;
        _logger = logger;
        _db = db;
    }

    public async Task Consume(ConsumeContext<GetStreamViewersRequestContract> context)
    {
        var request = context.Message;

        _logger.LogInformation("Processing GetStreamViewers request for StreamSessionId: {StreamSessionId}", 
            request.StreamSessionId);

        try
        {
            // First, we need to get all category IDs for this stream session
            // This is done through StreamCategories in TwitchTrackingService
            // However, we receive StreamCategoryIds in the request
            
            // For now, we'll get viewers by the categories associated with the stream
            // The Gateway will need to first get the stream details with categories,
            // then request viewers for those categories
            
            var viewers = new List<ViewerCategoryWatchDto>();

            if (request.StreamCategoryIds != null && request.StreamCategoryIds.Any())
            {
                var watches = await _categoryWatchRepository.GetByStreamCategoryIdsAsync(
                    request.StreamCategoryIds, 
                    context.CancellationToken);

                // Group by viewer and aggregate stats
                var viewerGroups = watches
                    .GroupBy(w => w.ViewerId)
                    .Select(g => new
                    {
                        Viewer = g.First().Viewer,
                        TotalMinutesWatched = g.Sum(w => w.MinutesWatched),
                        TotalChatPoints = g.Sum(w => w.ChatPoints),
                        TotalExperience = g.Sum(w => w.Experience)
                    })
                    .OrderByDescending(v => v.TotalExperience)
                    .ToList();

                viewers = viewerGroups.Select(v => new ViewerCategoryWatchDto
                {
                    Id = Guid.NewGuid(),
                    ViewerId = v.Viewer.Id,
                    StreamCategoryId = Guid.Empty, // Aggregated across all categories
                    MinutesWatched = v.TotalMinutesWatched,
                    ChatPoints = v.TotalChatPoints,
                    Experience = v.TotalExperience,
                    LastUpdatedAt = DateTime.UtcNow,
                    Viewer = new ViewerDto
                    {
                        Id = v.Viewer.Id,
                        TwitchUserId = v.Viewer.TwitchUserId,
                        DisplayName = v.Viewer.DisplayName,
                        Login = v.Viewer.Login,
                        ProfileImageUrl = v.Viewer.ProfileImageUrl,
                        CreatedAt = v.Viewer.CreatedAt
                    }
                }).ToList();
            }

            // Presence is session based and remains useful when there was no category.
            // Category-scoped requests provide their time bounds; older requests keep their old scope.
            if ((request.StreamCategoryIds?.Count ?? 0) == 0 || request.StartedAt.HasValue || request.IncludeSessionPresence)
            {
                var query = _db.CommunityPresences.AsNoTracking().Where(p => p.StreamSessionId == request.StreamSessionId);
                if (request.StartedAt.HasValue) query = query.Where(p => p.Minute >= request.StartedAt.Value);
                if (request.EndedAt.HasValue) query = query.Where(p => p.Minute < request.EndedAt.Value);
                var observed = await query.GroupBy(p => p.ViewerTwitchUserId)
                    .Select(g => new { UserId = g.Key, Minutes = g.Select(p => p.Minute).Distinct().Count() })
                    .ToListAsync(context.CancellationToken);
                var ids = observed.Select(p => p.UserId).ToList();
                var profiles = await _db.Viewers.AsNoTracking().Where(v => ids.Contains(v.TwitchUserId))
                    .ToDictionaryAsync(v => v.TwitchUserId, context.CancellationToken);
                foreach (var account in observed)
                {
                    var existing = viewers.FirstOrDefault(v => v.Viewer?.TwitchUserId == account.UserId);
                    if (existing != null)
                    {
                        // Both records can describe the same minute. Never add them together.
                        existing.MinutesWatched = Math.Max(existing.MinutesWatched, account.Minutes);
                        continue;
                    }
                    if (!profiles.TryGetValue(account.UserId, out var profile)) continue;
                    viewers.Add(new ViewerCategoryWatchDto
                    {
                        ViewerId = profile.Id, MinutesWatched = account.Minutes,
                        Viewer = new ViewerDto { Id = profile.Id, TwitchUserId = profile.TwitchUserId,
                            Login = profile.Login, DisplayName = profile.DisplayName, ProfileImageUrl = profile.ProfileImageUrl, CreatedAt = profile.CreatedAt }
                    });
                }
            }

            await context.RespondAsync(new GetStreamViewersResponseContract
            {
                Success = true,
                Viewers = viewers
            });

            _logger.LogInformation("Successfully processed GetStreamViewers request for StreamSessionId: {StreamSessionId}, found {Count} viewers",
                request.StreamSessionId, viewers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing GetStreamViewers request for StreamSessionId: {StreamSessionId}",
                request.StreamSessionId);

            await context.RespondAsync(new GetStreamViewersResponseContract
            {
                Success = false,
                Error = ex.Message,
                Viewers = new List<ViewerCategoryWatchDto>()
            });
        }
    }
}

