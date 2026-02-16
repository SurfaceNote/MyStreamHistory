using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Domain.Entities;
using MyStreamHistory.Shared.Application.UnitOfWork;

namespace MyStreamHistory.AuthService.Application.Services;

public class SocialLinkService(
    ISocialLinkRepository socialLinkRepository,
    ISocialLinkValidationService validationService,
    IUnitOfWork unitOfWork) : ISocialLinkService
{
    public async Task<List<SocialLink>> GetSocialLinksByUserIdAsync(Guid userId)
    {
        return await socialLinkRepository.GetByUserIdAsync(userId);
    }

    public async Task<(bool Success, string? ErrorMessage)> AddSocialLinkAsync(Guid userId, SocialNetworkType type, string path)
    {
        // Валидация пути
        if (!validationService.ValidateLink(type, path, out var validationError))
        {
            return (false, validationError);
        }

        // Проверка, что ссылка уже не существует
        var exists = await socialLinkRepository.ExistsAsync(userId, type);
        if (exists)
        {
            return (false, $"Social link for {type} already exists. Use update instead.");
        }

        var socialLink = new SocialLink
        {
            UserId = userId,
            SocialNetworkType = type,
            Path = path.Trim().TrimStart('/'),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await socialLinkRepository.AddAsync(socialLink);
        await unitOfWork.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateSocialLinkAsync(Guid userId, SocialNetworkType type, string path)
    {
        // Запрет на изменение Twitch
        if (type == SocialNetworkType.Twitch)
        {
            return (false, "Twitch link cannot be modified");
        }

        // Валидация пути
        if (!validationService.ValidateLink(type, path, out var validationError))
        {
            return (false, validationError);
        }

        var existingLink = await socialLinkRepository.GetByUserIdAndTypeAsync(userId, type);
        if (existingLink == null)
        {
            return (false, $"Social link for {type} not found. Use add instead.");
        }

        existingLink.Path = path.Trim().TrimStart('/');
        existingLink.UpdatedAt = DateTime.UtcNow;

        await socialLinkRepository.UpdateAsync(existingLink);
        await unitOfWork.SaveChangesAsync();

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteSocialLinkAsync(Guid userId, SocialNetworkType type)
    {
        // Запрет на удаление Twitch
        if (type == SocialNetworkType.Twitch)
        {
            return (false, "Twitch link cannot be deleted");
        }

        var existingLink = await socialLinkRepository.GetByUserIdAndTypeAsync(userId, type);
        if (existingLink == null)
        {
            return (false, $"Social link for {type} not found");
        }

        socialLinkRepository.Remove(existingLink);
        await unitOfWork.SaveChangesAsync();

        return (true, null);
    }
}

