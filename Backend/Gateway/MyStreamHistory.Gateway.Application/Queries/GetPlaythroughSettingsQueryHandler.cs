using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Requests;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Responses;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Queries;

public class GetPlaythroughSettingsQueryHandler(ITransportBus bus) : IRequestHandler<GetPlaythroughSettingsQuery, PlaythroughSettingsDto>
{
    public async Task<PlaythroughSettingsDto> Handle(GetPlaythroughSettingsQuery request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            GetPlaythroughSettingsRequestContract,
            GetPlaythroughSettingsResponseContract,
            BaseFailedResponseContract>(
            new GetPlaythroughSettingsRequestContract { TwitchUserId = request.TwitchUserId },
            cancellationToken);

        if (response.IsSuccess)
        {
            var settings = response.Success!.Settings;
            return new PlaythroughSettingsDto
            {
                Playthroughs = settings.Playthroughs.Select(playthrough => new PlaythroughDto
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
                    StreamCategories = playthrough.StreamCategories.Select(sc => new PlaythroughStreamCategoryDto
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
                }).ToList(),
                AvailableGames = settings.AvailableGames.Select(game => new PlaythroughGameOptionDto
                {
                    TwitchCategoryId = game.TwitchCategoryId,
                    TwitchCategoryTwitchId = game.TwitchCategoryTwitchId,
                    Name = game.Name,
                    BoxArtUrl = game.BoxArtUrl,
                    StreamCategories = game.StreamCategories.Select(sc => new PlaythroughStreamCategoryDto
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
                }).ToList()
            };
        }

        if (response.IsFailure)
        {
            throw new AppException(response.Failure!.Reason);
        }

        throw new InvalidOperationException();
    }
}
