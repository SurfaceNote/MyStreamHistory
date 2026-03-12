using MassTransit;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Requests;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Responses;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using ApplicationPlaythroughDto = MyStreamHistory.TwitchTrackingService.Application.DTOs.PlaythroughDto;
using ApplicationPlaythroughGameOptionDto = MyStreamHistory.TwitchTrackingService.Application.DTOs.PlaythroughGameOptionDto;
using ApplicationPlaythroughStreamCategoryDto = MyStreamHistory.TwitchTrackingService.Application.DTOs.PlaythroughStreamCategoryDto;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class GetPlaythroughSettingsConsumer(
    IPlaythroughService playthroughService,
    ILogger<GetPlaythroughSettingsConsumer> logger) : IConsumer<GetPlaythroughSettingsRequestContract>
{
    public async Task Consume(ConsumeContext<GetPlaythroughSettingsRequestContract> context)
    {
        try
        {
            var settings = await playthroughService.GetSettingsAsync(context.Message.TwitchUserId, context.CancellationToken);

            await context.RespondAsync(new GetPlaythroughSettingsResponseContract
            {
                Settings = new PlaythroughSettingsDto
                {
                    Playthroughs = settings.Playthroughs.Select(MapPlaythrough).ToList(),
                    AvailableGames = settings.AvailableGames.Select(MapGame).ToList()
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while getting playthrough settings for TwitchUserId {TwitchUserId}", context.Message.TwitchUserId);
            await context.RespondAsync(new BaseFailedResponseContract { Reason = ex.Message });
        }
    }

    private static PlaythroughDto MapPlaythrough(ApplicationPlaythroughDto playthrough)
    {
        return new PlaythroughDto
        {
            Id = playthrough.Id,
            TwitchCategoryId = playthrough.TwitchCategoryId,
            TwitchCategoryTwitchId = playthrough.TwitchCategoryTwitchId,
            GameName = playthrough.GameName,
            BoxArtUrl = playthrough.BoxArtUrl,
            Title = playthrough.Title,
            Status = playthrough.Status,
            AutoAddNewStreams = playthrough.AutoAddNewStreams,
            CreatedAt = playthrough.CreatedAt,
            UpdatedAt = playthrough.UpdatedAt,
            StreamCategories = playthrough.StreamCategories.Select(MapStreamCategory).ToList()
        };
    }

    private static PlaythroughGameOptionDto MapGame(ApplicationPlaythroughGameOptionDto game)
    {
        return new PlaythroughGameOptionDto
        {
            TwitchCategoryId = game.TwitchCategoryId,
            TwitchCategoryTwitchId = game.TwitchCategoryTwitchId,
            Name = game.Name,
            BoxArtUrl = game.BoxArtUrl,
            StreamCategories = game.StreamCategories.Select(MapStreamCategory).ToList()
        };
    }

    private static PlaythroughStreamCategoryDto MapStreamCategory(ApplicationPlaythroughStreamCategoryDto streamCategory)
    {
        return new PlaythroughStreamCategoryDto
        {
            StreamCategoryId = streamCategory.StreamCategoryId,
            StreamSessionId = streamCategory.StreamSessionId,
            TwitchCategoryId = streamCategory.TwitchCategoryId,
            GameName = streamCategory.GameName,
            StreamTitle = streamCategory.StreamTitle,
            StreamStartedAt = streamCategory.StreamStartedAt,
            StreamEndedAt = streamCategory.StreamEndedAt,
            CategoryStartedAt = streamCategory.CategoryStartedAt,
            CategoryEndedAt = streamCategory.CategoryEndedAt
        };
    }
}
