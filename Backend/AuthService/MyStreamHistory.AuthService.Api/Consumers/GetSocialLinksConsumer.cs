using MassTransit;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Auth;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;
using MyStreamHistory.Shared.Base.Contracts.Auth.Responses;

namespace MyStreamHistory.AuthService.Api.Consumers;

public class GetSocialLinksConsumer : IConsumer<GetSocialLinksRequestContract>
{
    private readonly ISocialLinkService _socialLinkService;
    private readonly ILogger<GetSocialLinksConsumer> _logger;

    public GetSocialLinksConsumer(ISocialLinkService socialLinkService, ILogger<GetSocialLinksConsumer> logger)
    {
        _socialLinkService = socialLinkService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GetSocialLinksRequestContract> context)
    {
        _logger.LogInformation("Received request to get social links for user {UserId}", context.Message.UserId);

        try
        {
            var socialLinks = await _socialLinkService.GetSocialLinksByUserIdAsync(context.Message.UserId);

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
            _logger.LogInformation("Successfully responded with {Count} social links for user {UserId}", 
                socialLinks.Count, context.Message.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting social links for user {UserId}", context.Message.UserId);
            
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

