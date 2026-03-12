using MassTransit;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Requests;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Responses;
using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using ContractPlaythroughDto = MyStreamHistory.Shared.Base.Contracts.Playthroughs.PlaythroughDto;
using ContractPlaythroughStreamCategoryDto = MyStreamHistory.Shared.Base.Contracts.Playthroughs.PlaythroughStreamCategoryDto;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class UpsertPlaythroughConsumer(
    IPlaythroughService playthroughService,
    ILogger<UpsertPlaythroughConsumer> logger) : IConsumer<UpsertPlaythroughRequestContract>
{
    public async Task Consume(ConsumeContext<UpsertPlaythroughRequestContract> context)
    {
        try
        {
            var playthrough = await playthroughService.UpsertAsync(context.Message.TwitchUserId, new UpsertPlaythroughRequestDto
            {
                Id = context.Message.Id,
                TwitchCategoryId = context.Message.TwitchCategoryId,
                Title = context.Message.Title,
                Status = context.Message.Status,
                AutoAddNewStreams = context.Message.AutoAddNewStreams,
                StreamCategoryIds = context.Message.StreamCategoryIds
            }, context.CancellationToken);

            await context.RespondAsync(new UpsertPlaythroughResponseContract
            {
                Playthrough = new ContractPlaythroughDto
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
                    StreamCategories = playthrough.StreamCategories.Select(sc => new ContractPlaythroughStreamCategoryDto
                    {
                        StreamCategoryId = sc.StreamCategoryId,
                        StreamSessionId = sc.StreamSessionId,
                        TwitchCategoryId = sc.TwitchCategoryId,
                        GameName = sc.GameName,
                        StreamTitle = sc.StreamTitle,
                        StreamStartedAt = sc.StreamStartedAt,
                        StreamEndedAt = sc.StreamEndedAt,
                        CategoryStartedAt = sc.CategoryStartedAt,
                        CategoryEndedAt = sc.CategoryEndedAt
                    }).ToList()
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while saving playthrough for TwitchUserId {TwitchUserId}", context.Message.TwitchUserId);
            await context.RespondAsync(new BaseFailedResponseContract { Reason = ex.Message });
        }
    }
}
