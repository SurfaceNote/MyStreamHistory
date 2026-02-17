using MassTransit;
using Microsoft.EntityFrameworkCore;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class GetStreamerStatisticsConsumer : IConsumer<GetStreamerStatisticsRequestContract>
{
    private readonly IStreamSessionRepository _streamSessionRepository;
    private readonly ITwitchCategoryRepository _twitchCategoryRepository;
    private readonly IStreamCategoryRepository _streamCategoryRepository;
    private readonly ILogger<GetStreamerStatisticsConsumer> _logger;

    public GetStreamerStatisticsConsumer(
        IStreamSessionRepository streamSessionRepository,
        ITwitchCategoryRepository twitchCategoryRepository,
        IStreamCategoryRepository streamCategoryRepository,
        ILogger<GetStreamerStatisticsConsumer> logger)
    {
        _streamSessionRepository = streamSessionRepository;
        _twitchCategoryRepository = twitchCategoryRepository;
        _streamCategoryRepository = streamCategoryRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetStreamerStatisticsRequestContract> context)
    {
        _logger.LogInformation("Received request to get streamer statistics for TwitchUserId {TwitchUserId}", 
            context.Message.TwitchUserId);

        try
        {
            // Get all stream sessions for the user
            var allSessions = await _streamSessionRepository.Query()
                .Where(s => s.TwitchUserId == context.Message.TwitchUserId)
                .Include(s => s.StreamCategories)
                .ToListAsync(context.CancellationToken);

            // Calculate total streams count
            var totalStreamsCount = allSessions.Count;

            // Calculate total streamed hours
            var totalStreamedHours = allSessions
                .Where(s => s.EndedAt.HasValue)
                .Sum(s => (s.EndedAt!.Value - s.StartedAt).TotalHours);

            // Get all unique category IDs from stream categories
            var uniqueCategoryIds = allSessions
                .SelectMany(s => s.StreamCategories)
                .Select(sc => sc.TwitchCategoryId)
                .Distinct()
                .ToList();

            var totalUniqueGamesCount = uniqueCategoryIds.Count;

            // Get category details with time spent
            var categories = new List<CategoryStatisticsDto>();
            
            foreach (var categoryId in uniqueCategoryIds)
            {
                var category = await _twitchCategoryRepository.Query()
                    .FirstOrDefaultAsync(c => c.Id == categoryId, context.CancellationToken);

                if (category == null)
                    continue;

                // Calculate total hours for this category
                var categoryStreamCategories = await _streamCategoryRepository.Query()
                    .Where(sc => sc.TwitchCategoryId == categoryId)
                    .Include(sc => sc.StreamSession)
                    .Where(sc => sc.StreamSession.TwitchUserId == context.Message.TwitchUserId)
                    .ToListAsync(context.CancellationToken);

                var totalHours = categoryStreamCategories
                    .Where(sc => sc.EndedAt.HasValue)
                    .Sum(sc => (sc.EndedAt!.Value - sc.StartedAt).TotalHours);

                categories.Add(new CategoryStatisticsDto
                {
                    TwitchCategoryId = category.Id,
                    TwitchId = category.TwitchId,
                    Name = category.Name,
                    BoxArtUrl = category.BoxArtUrl,
                    IgdbId = category.IgdbId,
                    TotalHours = Math.Round(totalHours, 1)
                });
            }

            // Sort categories by total hours descending
            categories = categories.OrderByDescending(c => c.TotalHours).ToList();

            var response = new GetStreamerStatisticsResponseContract
            {
                TotalStreamsCount = totalStreamsCount,
                TotalUniqueGamesCount = totalUniqueGamesCount,
                TotalStreamedHours = Math.Round(totalStreamedHours, 1),
                Categories = categories
            };

            await context.RespondAsync(response);
            _logger.LogInformation("Successfully responded with statistics for TwitchUserId {TwitchUserId}", 
                context.Message.TwitchUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting streamer statistics for TwitchUserId {TwitchUserId}", 
                context.Message.TwitchUserId);
            
            await context.RespondAsync(new BaseFailedResponseContract
            {
                Reason = $"Error getting streamer statistics: {ex.Message}"
            });
        }
    }
}

