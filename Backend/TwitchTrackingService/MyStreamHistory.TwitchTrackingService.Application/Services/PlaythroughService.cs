using MyStreamHistory.Shared.Application.UnitOfWork;
using MyStreamHistory.Shared.Base.Exceptions;
using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;

namespace MyStreamHistory.TwitchTrackingService.Application.Services;

public class PlaythroughService(
    IPlaythroughRepository playthroughRepository,
    IPlaythroughStreamCategoryRepository playthroughStreamCategoryRepository,
    IStreamCategoryRepository streamCategoryRepository,
    ITwitchCategoryRepository twitchCategoryRepository,
    IUnitOfWork unitOfWork) : IPlaythroughService
{
    public async Task<PlaythroughSettingsDto> GetSettingsAsync(int twitchUserId, CancellationToken cancellationToken = default)
    {
        var playthroughs = await playthroughRepository.GetByTwitchUserIdAsync(twitchUserId, cancellationToken);
        var assignedStreamCategoryIds = await playthroughStreamCategoryRepository.GetAssignedStreamCategoryIdsAsync(cancellationToken);

        var availableGames = await streamCategoryRepository.GetByTwitchUserIdAsync(twitchUserId, cancellationToken);
        var availableStreamCategories = availableGames
            .Where(sc => !assignedStreamCategoryIds.Contains(sc.Id))
            .ToList();

        return new PlaythroughSettingsDto
        {
            Playthroughs = playthroughs.Select(MapPlaythrough).ToList(),
            AvailableGames = availableStreamCategories
                .GroupBy(sc => sc.TwitchCategoryId)
                .Select(group =>
                {
                    var category = group.First().TwitchCategory;

                    return new PlaythroughGameOptionDto
                    {
                        TwitchCategoryId = category.Id,
                        TwitchCategoryTwitchId = category.TwitchId,
                        Name = category.Name,
                        BoxArtUrl = category.BoxArtUrl,
                        StreamCategories = group
                            .OrderByDescending(sc => sc.StartedAt)
                            .Select(MapStreamCategory)
                            .ToList()
                    };
                })
                .OrderByDescending(game => game.StreamCategories.Max(sc => sc.CategoryStartedAt))
                .ToList()
        };
    }

    public async Task<PlaythroughDto> UpsertAsync(int twitchUserId, UpsertPlaythroughRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<PlaythroughStatus>(request.Status, true, out var status))
        {
            throw new AppException($"Unsupported playthrough status: {request.Status}");
        }

        var twitchCategory = await twitchCategoryRepository.GetByIdAsync(request.TwitchCategoryId, cancellationToken);
        if (twitchCategory == null)
        {
            throw new AppException("Game was not found.");
        }

        var selectedStreamCategories = await LoadAndValidateStreamCategoriesAsync(
            twitchUserId,
            request.TwitchCategoryId,
            request.StreamCategoryIds,
            cancellationToken);

        await EnsureSingleAutoAddPlaythroughAsync(
            twitchUserId,
            request.TwitchCategoryId,
            request.Id,
            request.AutoAddNewStreams,
            cancellationToken);

        Playthrough playthrough;

        if (request.Id.HasValue)
        {
            playthrough = await playthroughRepository.GetByIdAsync(request.Id.Value, cancellationToken)
                ?? throw new AppException("Playthrough was not found.");

            if (playthrough.TwitchUserId != twitchUserId)
            {
                throw new AppException("You cannot edit this playthrough.");
            }

            playthrough.TwitchCategoryId = request.TwitchCategoryId;
            playthrough.Title = BuildTitle(request.Title, twitchCategory.Name);
            playthrough.Status = status;
            playthrough.AutoAddNewStreams = request.AutoAddNewStreams;
            playthrough.UpdatedAt = DateTime.UtcNow;

            await SyncPlaythroughStreamCategoriesAsync(playthrough.Id, selectedStreamCategories, cancellationToken);
            await playthroughRepository.UpdateAsync(playthrough, cancellationToken);
        }
        else
        {
            playthrough = new Playthrough
            {
                TwitchUserId = twitchUserId,
                TwitchCategoryId = request.TwitchCategoryId,
                Title = BuildTitle(request.Title, twitchCategory.Name),
                Status = status,
                AutoAddNewStreams = request.AutoAddNewStreams,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await playthroughRepository.AddAsync(playthrough, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var newLinks = selectedStreamCategories
                .Select(sc => new PlaythroughStreamCategory
                {
                    PlaythroughId = playthrough.Id,
                    StreamCategoryId = sc.Id,
                    AddedAt = DateTime.UtcNow
                })
                .ToList();

            await playthroughStreamCategoryRepository.AddRangeAsync(newLinks, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var savedPlaythrough = await playthroughRepository.GetByIdAsync(playthrough.Id, cancellationToken)
            ?? throw new AppException("Playthrough was not found after saving.");

        return MapPlaythrough(savedPlaythrough);
    }

    public async Task DeleteAsync(int twitchUserId, Guid playthroughId, CancellationToken cancellationToken = default)
    {
        var playthrough = await playthroughRepository.GetByIdAsync(playthroughId, cancellationToken)
            ?? throw new AppException("Playthrough was not found.");

        if (playthrough.TwitchUserId != twitchUserId)
        {
            throw new AppException("You cannot delete this playthrough.");
        }

        await playthroughRepository.DeleteAsync(playthrough, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AutoAttachStreamCategoryAsync(int twitchUserId, Guid streamCategoryId, Guid twitchCategoryId, CancellationToken cancellationToken = default)
    {
        var activeAutoPlaythrough = await playthroughRepository.GetAutoAddPlaythroughIdAsync(twitchUserId, twitchCategoryId, cancellationToken);

        if (!activeAutoPlaythrough.HasValue)
        {
            return;
        }

        var alreadyLinked = await playthroughStreamCategoryRepository.ExistsAsync(
            activeAutoPlaythrough.Value,
            streamCategoryId,
            cancellationToken);

        if (alreadyLinked)
        {
            return;
        }

        await playthroughStreamCategoryRepository.AddRangeAsync(new List<PlaythroughStreamCategory>
        {
            new()
            {
                PlaythroughId = activeAutoPlaythrough.Value,
                StreamCategoryId = streamCategoryId,
                AddedAt = DateTime.UtcNow
            }
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<StreamCategory>> LoadAndValidateStreamCategoriesAsync(
        int twitchUserId,
        Guid twitchCategoryId,
        List<Guid> streamCategoryIds,
        CancellationToken cancellationToken)
    {
        if (streamCategoryIds.Count == 0)
        {
            return new List<StreamCategory>();
        }

        var uniqueIds = streamCategoryIds.Distinct().ToList();

        var streamCategories = await streamCategoryRepository.GetByIdsAsync(uniqueIds, cancellationToken);

        if (streamCategories.Count != uniqueIds.Count)
        {
            throw new AppException("Some selected stream categories were not found.");
        }

        if (streamCategories.Any(sc => sc.StreamSession.TwitchUserId != twitchUserId))
        {
            throw new AppException("Some selected stream categories do not belong to the current user.");
        }

        if (streamCategories.Any(sc => sc.TwitchCategoryId != twitchCategoryId))
        {
            throw new AppException("A playthrough can contain only one game.");
        }

        return streamCategories;
    }

    private async Task EnsureSingleAutoAddPlaythroughAsync(
        int twitchUserId,
        Guid twitchCategoryId,
        Guid? currentPlaythroughId,
        bool autoAddNewStreams,
        CancellationToken cancellationToken)
    {
        if (!autoAddNewStreams)
        {
            return;
        }

        var conflictingPlaythrough = await playthroughRepository.HasConflictingAutoAddAsync(
            twitchUserId,
            twitchCategoryId,
            currentPlaythroughId,
            cancellationToken);

        if (conflictingPlaythrough)
        {
            throw new AppException("Only one playthrough per game can auto-add new streams.");
        }
    }

    private async Task SyncPlaythroughStreamCategoriesAsync(
        Guid playthroughId,
        List<StreamCategory> selectedStreamCategories,
        CancellationToken cancellationToken)
    {
        var existingLinks = await playthroughStreamCategoryRepository.GetByPlaythroughIdAsync(playthroughId, cancellationToken);
        var selectedIds = selectedStreamCategories.Select(sc => sc.Id).ToHashSet();

        var linksToRemove = existingLinks
            .Where(link => !selectedIds.Contains(link.StreamCategoryId))
            .ToList();

        var existingIds = existingLinks.Select(link => link.StreamCategoryId).ToHashSet();
        var linksToAdd = selectedStreamCategories
            .Where(sc => !existingIds.Contains(sc.Id))
            .Select(sc => new PlaythroughStreamCategory
            {
                PlaythroughId = playthroughId,
                StreamCategoryId = sc.Id,
                AddedAt = DateTime.UtcNow
            })
            .ToList();

        await playthroughStreamCategoryRepository.RemoveRangeAsync(linksToRemove, cancellationToken);
        await playthroughStreamCategoryRepository.AddRangeAsync(linksToAdd, cancellationToken);
    }

    private static PlaythroughDto MapPlaythrough(Playthrough playthrough)
    {
        return new PlaythroughDto
        {
            Id = playthrough.Id,
            TwitchCategoryId = playthrough.TwitchCategoryId,
            TwitchCategoryTwitchId = playthrough.TwitchCategory.TwitchId,
            GameName = playthrough.TwitchCategory.Name,
            BoxArtUrl = playthrough.TwitchCategory.BoxArtUrl,
            Title = playthrough.Title,
            Status = playthrough.Status.ToString(),
            AutoAddNewStreams = playthrough.AutoAddNewStreams,
            CreatedAt = playthrough.CreatedAt,
            UpdatedAt = playthrough.UpdatedAt,
            StreamCategories = playthrough.PlaythroughStreamCategories
                .OrderByDescending(link => link.StreamCategory.StartedAt)
                .Select(link => MapStreamCategory(link.StreamCategory))
                .ToList()
        };
    }

    private static PlaythroughStreamCategoryDto MapStreamCategory(StreamCategory streamCategory)
    {
        return new PlaythroughStreamCategoryDto
        {
            StreamCategoryId = streamCategory.Id,
            StreamSessionId = streamCategory.StreamSessionId,
            TwitchCategoryId = streamCategory.TwitchCategoryId,
            GameName = streamCategory.TwitchCategory.Name,
            StreamTitle = streamCategory.StreamSession.StreamTitle ?? string.Empty,
            StreamStartedAt = streamCategory.StreamSession.StartedAt,
            StreamEndedAt = streamCategory.StreamSession.EndedAt,
            CategoryStartedAt = streamCategory.StartedAt,
            CategoryEndedAt = streamCategory.EndedAt
        };
    }

    private static string BuildTitle(string title, string gameName)
    {
        return string.IsNullOrWhiteSpace(title)
            ? $"{gameName} {DateTime.UtcNow:yyyy-MM-dd}"
            : title.Trim();
    }
}
