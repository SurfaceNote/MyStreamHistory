using MediatR;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Requests;
using MyStreamHistory.Shared.Base.Contracts.Playthroughs.Responses;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Commands;

public class UpsertPlaythroughCommandHandler(ITransportBus bus) : IRequestHandler<UpsertPlaythroughCommand, PlaythroughDto>
{
    public async Task<PlaythroughDto> Handle(UpsertPlaythroughCommand request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<
            UpsertPlaythroughRequestContract,
            UpsertPlaythroughResponseContract,
            BaseFailedResponseContract>(
            new UpsertPlaythroughRequestContract
            {
                Id = request.Request.Id,
                TwitchUserId = request.TwitchUserId,
                TwitchCategoryId = request.Request.TwitchCategoryId,
                Title = request.Request.Title,
                Status = request.Request.Status,
                AutoAddNewStreams = request.Request.AutoAddNewStreams,
                StreamCategoryIds = request.Request.StreamCategoryIds
            },
            cancellationToken);

        if (response.IsSuccess)
        {
            var playthrough = response.Success!.Playthrough;
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
            };
        }

        if (response.IsFailure)
        {
            throw new AppException(response.Failure!.Reason);
        }

        throw new InvalidOperationException();
    }
}
