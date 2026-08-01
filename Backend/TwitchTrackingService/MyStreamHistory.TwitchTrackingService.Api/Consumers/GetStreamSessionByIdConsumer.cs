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
            var now = DateTime.UtcNow;
            var session = await _streamSessionRepository.Query()
                .AsNoTracking()
                .Where(s => s.Id == context.Message.StreamSessionId)
                .Select(s => new StreamSessionDetailsDto
                {
                    Id = s.Id,
                    StreamId = s.StreamId,
                    TwitchUserId = s.TwitchUserId,
                    StreamerLogin = s.StreamerLogin,
                    StreamerDisplayName = s.StreamerDisplayName,
                    StreamerAvatarUrl = s.StreamerAvatarUrl,
                    StartedAt = s.StartedAt,
                    EndedAt = s.EndedAt,
                    IsLive = s.IsLive,
                    StreamTitle = s.StreamTitle,
                    GameName = s.GameName,
                    ViewerCount = s.ViewerCount,
                    Categories = s.StreamCategories
                        .OrderBy(sc => sc.StartedAt)
                        .Select(sc => new StreamCategoryDetailsDto
                        {
                            StreamCategoryId = sc.Id,
                            TwitchCategoryId = sc.TwitchCategory.TwitchId,
                            Name = sc.TwitchCategory.Name,
                            BoxArtUrl = sc.TwitchCategory.BoxArtUrl,
                            StartedAt = sc.StartedAt,
                            EndedAt = sc.EndedAt
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync(context.CancellationToken);

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

            foreach (var category in session.Categories)
            {
                category.DurationMinutes = (int)((category.EndedAt ?? now) - category.StartedAt).TotalMinutes;
            }

            // Get actual streamer profile from AuthService
            var userProfile = await _userProfileService.GetUserProfileAsync(session.TwitchUserId, context.CancellationToken);
            
            var response = new GetStreamSessionByIdResponseContract
            {
                Success = true,
                StreamSession = session
            };

            session.StreamerDisplayName = userProfile?.DisplayName ?? session.StreamerDisplayName;
            session.StreamerAvatarUrl = userProfile?.Avatar ?? session.StreamerAvatarUrl;

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

