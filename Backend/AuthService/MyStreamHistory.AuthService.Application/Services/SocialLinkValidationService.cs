using System.Text.RegularExpressions;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Domain.Entities;

namespace MyStreamHistory.AuthService.Application.Services;

public class SocialLinkValidationService : ISocialLinkValidationService
{
    public bool ValidateLink(SocialNetworkType type, string path, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = "Path cannot be empty";
            return false;
        }

        // Удаляем начальные и конечные пробелы
        path = path.Trim();

        // Проверяем, что путь не содержит полный URL (только путь после домена)
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Please enter only the path after the domain (e.g., 'username' instead of 'https://site.com/username')";
            return false;
        }

        // Удаляем начальный слэш если есть (но не для telegram с плюсом)
        if (path.StartsWith('/') && !path.StartsWith('+'))
        {
            path = path.Substring(1);
        }

        // Базовая проверка на допустимые символы (буквы, цифры, дефисы, подчеркивания, слэши, плюс для Telegram)
        var validPathRegex = new Regex(@"^[a-zA-Z0-9_\-/\.@\+]+$");
        if (!validPathRegex.IsMatch(path))
        {
            errorMessage = "Path contains invalid characters. Only letters, numbers, hyphens, underscores, slashes, dots, @, and + are allowed";
            return false;
        }

        // Специфичные проверки для каждой соцсети
        switch (type)
        {
            case SocialNetworkType.Twitch:
                // Twitch username: только буквы, цифры и подчеркивания, 4-25 символов
                if (!Regex.IsMatch(path, @"^[a-zA-Z0-9_]{4,25}$"))
                {
                    errorMessage = "Twitch username must be 4-25 characters and contain only letters, numbers, and underscores";
                    return false;
                }
                break;

            case SocialNetworkType.YouTube:
                // YouTube может быть: @username, c/channelname, channel/UCxxxxx, user/username
                if (!Regex.IsMatch(path, @"^(@[a-zA-Z0-9_\-\.]+|c/[a-zA-Z0-9_\-]+|channel/UC[a-zA-Z0-9_\-]+|user/[a-zA-Z0-9_\-]+)$"))
                {
                    errorMessage = "YouTube path must be in format: @username, c/channelname, channel/UCxxxxx, or user/username";
                    return false;
                }
                break;

            case SocialNetworkType.Instagram:
                // Instagram username: буквы, цифры, точки, подчеркивания, до 30 символов
                if (!Regex.IsMatch(path, @"^[a-zA-Z0-9_\.]{1,30}$"))
                {
                    errorMessage = "Instagram username must be 1-30 characters and contain only letters, numbers, dots, and underscores";
                    return false;
                }
                break;

            case SocialNetworkType.Discord:
                // Discord invite code или username
                if (!Regex.IsMatch(path, @"^[a-zA-Z0-9_\-]{2,32}$"))
                {
                    errorMessage = "Discord invite code or username must be 2-32 characters";
                    return false;
                }
                break;

            case SocialNetworkType.Steam:
                // Steam может быть: id/username или profiles/steamid64
                if (!Regex.IsMatch(path, @"^(id/[a-zA-Z0-9_\-]+|profiles/[0-9]{17})$"))
                {
                    errorMessage = "Steam path must be in format: id/username or profiles/steamid64";
                    return false;
                }
                break;

            case SocialNetworkType.VK:
                // VK может быть: username или id123456
                if (!Regex.IsMatch(path, @"^([a-zA-Z0-9_\.]+|id[0-9]+)$"))
                {
                    errorMessage = "VK path must be a username or id (e.g., 'username' or 'id123456')";
                    return false;
                }
                break;

            case SocialNetworkType.Yandex:
                // Yandex Dzen username
                if (!Regex.IsMatch(path, @"^[a-zA-Z0-9_\-\.]+$"))
                {
                    errorMessage = "Yandex path must contain only letters, numbers, hyphens, underscores, and dots";
                    return false;
                }
                break;

            case SocialNetworkType.Telegram:
                // Telegram может быть: username (без @), +code (любая длина), joinchat/code, или просто путь
                if (!Regex.IsMatch(path, @"^([a-zA-Z0-9_]{5,32}|\+[a-zA-Z0-9_\-]+|joinchat/[a-zA-Z0-9_\-]+|[a-zA-Z0-9_/\-]+)$"))
                {
                    errorMessage = "Telegram path must be a username (5-32 chars), channel (+code), or chat link (joinchat/code)";
                    return false;
                }
                break;
        }

        return true;
    }
}

