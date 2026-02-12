using MassTransit;
using Microsoft.EntityFrameworkCore;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class GetStreamSessionByIdConsumer : IConsumer<GetStreamSessionByIdRequestContract>
{
    private readonly IStreamSessionRepository _streamSessionRepository;
    private readonly IUserProfileService _userProfileService;
    private readonly TwitchTrackingDbContext _dbContext;
    private readonly ILogger<GetStreamSessionByIdConsumer> _logger;

    public GetStreamSessionByIdConsumer(
        IStreamSessionRepository streamSessionRepository,
        IUserProfileService userProfileService,
        TwitchTrackingDbContext dbContext,
        ILogger<GetStreamSessionByIdConsumer> logger)
    {
        _streamSessionRepository = streamSessionRepository;
        _userProfileService = userProfileService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetStreamSessionByIdRequestContract> context)
    {
        _logger.LogInformation("Received request to get stream session by ID {StreamSessionId}", 
            context.Message.StreamSessionId);

        try
        {
            var session = await _streamSessionRepository.Query()
                .Include(s => s.StreamCategories)
                .ThenInclude(sc => sc.TwitchCategory)
                .FirstOrDefaultAsync(s => s.Id == context.Message.StreamSessionId, context.CancellationToken);

            if (session == null)
            {
                _logger.LogWarning("Stream session not found for ID {StreamSessionId}", 
                    context.Message.StreamSessionId);
                
                await context.RespondAsync(new GetStreamSessionByIdResponseContract
                {
                    Success = false,
                    Error = "Stream session not found"
                });
                return;
            }

            // Get actual streamer profile from AuthService
            var userProfile = await _userProfileService.GetUserProfileAsync(session.TwitchUserId, context.CancellationToken);
            
            var categories = session.StreamCategories
                .OrderBy(sc => sc.StartedAt)
                .Select(sc => new StreamCategoryDetailsDto
                {
                    StreamCategoryId = sc.Id,
                    TwitchCategoryId = sc.TwitchCategory.TwitchId,
                    Name = sc.TwitchCategory.Name,
                    BoxArtUrl = sc.TwitchCategory.BoxArtUrl,
                    StartedAt = sc.StartedAt,
                    EndedAt = sc.EndedAt,
                    DurationMinutes = sc.EndedAt.HasValue 
                        ? (int)(sc.EndedAt.Value - sc.StartedAt).TotalMinutes
                        : (int)(DateTime.UtcNow - sc.StartedAt).TotalMinutes
                })
                .ToList();

            var response = new GetStreamSessionByIdResponseContract
            {
                Success = true,
                StreamSession = new StreamSessionDetailsDto
                {
                    Id = session.Id,
                    StreamId = session.StreamId,
                    TwitchUserId = session.TwitchUserId,
                    // Use actual data from AuthService with fallback to StreamSession
                    StreamerLogin = session.StreamerLogin,
                    StreamerDisplayName = userProfile?.DisplayName ?? session.StreamerDisplayName,
                    StreamerAvatarUrl = userProfile?.Avatar ?? session.StreamerAvatarUrl,
                    StartedAt = session.StartedAt,
                    EndedAt = session.EndedAt,
                    IsLive = session.IsLive,
                    StreamTitle = session.StreamTitle,
                    GameName = session.GameName,
                    ViewerCount = session.ViewerCount,
                    Categories = categories
                }
            };

            await context.RespondAsync(response);
            _logger.LogInformation("Successfully responded with stream session details for ID {StreamSessionId}", 
                context.Message.StreamSessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting stream session by ID {StreamSessionId}", 
                context.Message.StreamSessionId);
            
            await context.RespondAsync(new GetStreamSessionByIdResponseContract
            {
                Success = false,
                Error = $"Error getting stream session: {ex.Message}"
            });
        }
    }
}

