using MassTransit;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Auth;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;
using MyStreamHistory.Shared.Base.Contracts.Auth.Responses;

namespace MyStreamHistory.AuthService.Api.Consumers;

public class GetSocialLinksByTwitchIdConsumer : IConsumer<GetSocialLinksByTwitchIdRequestContract>
{
    private readonly ISocialLinkService _socialLinkService;
    private readonly IAuthUserRepository _authUserRepository;
    private readonly ILogger<GetSocialLinksByTwitchIdConsumer> _logger;

    public GetSocialLinksByTwitchIdConsumer(
        ISocialLinkService socialLinkService, 
        IAuthUserRepository authUserRepository,
        ILogger<GetSocialLinksByTwitchIdConsumer> logger)
    {
        _socialLinkService = socialLinkService;
        _authUserRepository = authUserRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetSocialLinksByTwitchIdRequestContract> context)
    {
        _logger.LogInformation("Received request to get social links for TwitchId {TwitchId}", context.Message.TwitchId);

        try
        {
            // Найти пользователя по TwitchId
            var user = await _authUserRepository.GetUserByTwitchIdAsync(context.Message.TwitchId);
            
            if (user == null)
            {
                _logger.LogWarning("User with TwitchId {TwitchId} not found", context.Message.TwitchId);
                await context.RespondAsync(new BaseFailedResponseContract
                {
                    Reason = $"User with TwitchId {context.Message.TwitchId} not found"
                });
                return;
            }

            // Получить социальные ссылки пользователя
            var socialLinks = await _socialLinkService.GetSocialLinksByUserIdAsync(user.Id);

            var response = new GetSocialLinksResponseContract
            {
                SocialLinks = socialLinks.Select(sl => new SocialLinkDto
                {
                    SocialNetworkType = sl.SocialNetworkType.ToString(),
                    Path = sl.Path,
                    FullUrl = GetFullUrl(sl.SocialNetworkType.ToString(), sl.Path)
                }).ToList()
            };

            await context.RespondAsync(response);
            _logger.LogInformation("Successfully responded with {Count} social links for TwitchId {TwitchId}", 
                socialLinks.Count, context.Message.TwitchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting social links for TwitchId {TwitchId}", context.Message.TwitchId);
            
            await context.RespondAsync(new BaseFailedResponseContract
            {
                Reason = $"Error getting social links: {ex.Message}"
            });
        }
    }

    private static string GetFullUrl(string socialNetworkType, string path)
    {
        return socialNetworkType switch
        {
            "Twitch" => $"https://twitch.tv/{path}",
            "YouTube" => $"https://youtube.com/{path}",
            "Instagram" => $"https://instagram.com/{path}",
            "Discord" => $"https://discord.gg/{path}",
            "Steam" => $"https://steamcommunity.com/{path}",
            "VK" => $"https://vk.com/{path}",
            "Yandex" => $"https://dzen.ru/{path}",
            "Telegram" => $"https://t.me/{path}",
            _ => path
        };
    }
}

