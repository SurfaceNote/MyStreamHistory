using MyStreamHistory.TwitchTrackingService.Application.DTOs;

namespace MyStreamHistory.TwitchTrackingService.Application.Interfaces;

public interface IPlaythroughService
{
    Task<PlaythroughSettingsDto> GetSettingsAsync(int twitchUserId, CancellationToken cancellationToken = default);
    Task<PlaythroughDto> UpsertAsync(int twitchUserId, UpsertPlaythroughRequestDto request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int twitchUserId, Guid playthroughId, CancellationToken cancellationToken = default);
    Task AutoAttachStreamCategoryAsync(int twitchUserId, Guid streamCategoryId, Guid twitchCategoryId, CancellationToken cancellationToken = default);
}
