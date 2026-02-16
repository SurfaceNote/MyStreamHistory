using MassTransit;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Domain.Entities;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;
using MyStreamHistory.Shared.Base.Contracts.Auth.Responses;

namespace MyStreamHistory.AuthService.Api.Consumers;

public class UpdateSocialLinksConsumer : IConsumer<UpdateSocialLinksRequestContract>
{
    private readonly ISocialLinkService _socialLinkService;
    private readonly ILogger<UpdateSocialLinksConsumer> _logger;

    public UpdateSocialLinksConsumer(ISocialLinkService socialLinkService, ILogger<UpdateSocialLinksConsumer> logger)
    {
        _socialLinkService = socialLinkService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UpdateSocialLinksRequestContract> context)
    {
        _logger.LogInformation("Received request to update social links for user {UserId}", context.Message.UserId);

        try
        {
            var existingLinks = await _socialLinkService.GetSocialLinksByUserIdAsync(context.Message.UserId);
            var requestedTypes = context.Message.SocialLinks
                .Select(sl => Enum.Parse<SocialNetworkType>(sl.SocialNetworkType))
                .ToHashSet();

            // Обработка каждой ссылки из запроса
            foreach (var linkDto in context.Message.SocialLinks)
            {
                if (!Enum.TryParse<SocialNetworkType>(linkDto.SocialNetworkType, out var type))
                {
                    _logger.LogWarning("Invalid social network type: {Type}", linkDto.SocialNetworkType);
                    await context.RespondAsync(new UpdateSocialLinksResponseContract
                    {
                        Success = false,
                        Error = $"Invalid social network type: {linkDto.SocialNetworkType}"
                    });
                    return;
                }

                var existingLink = existingLinks.FirstOrDefault(el => el.SocialNetworkType == type);

                if (existingLink != null)
                {
                    // Обновление существующей ссылки
                    var (success, error) = await _socialLinkService.UpdateSocialLinkAsync(
                        context.Message.UserId, type, linkDto.Path);

                    if (!success)
                    {
                        _logger.LogWarning("Failed to update social link {Type}: {Error}", type, error);
                        await context.RespondAsync(new UpdateSocialLinksResponseContract
                        {
                            Success = false,
                            Error = error
                        });
                        return;
                    }
                }
                else
                {
                    // Добавление новой ссылки
                    var (success, error) = await _socialLinkService.AddSocialLinkAsync(
                        context.Message.UserId, type, linkDto.Path);

                    if (!success)
                    {
                        _logger.LogWarning("Failed to add social link {Type}: {Error}", type, error);
                        await context.RespondAsync(new UpdateSocialLinksResponseContract
                        {
                            Success = false,
                            Error = error
                        });
                        return;
                    }
                }
            }

            // Удаление ссылок, которых нет в запросе (кроме Twitch)
            foreach (var existingLink in existingLinks)
            {
                if (!requestedTypes.Contains(existingLink.SocialNetworkType) && 
                    existingLink.SocialNetworkType != SocialNetworkType.Twitch)
                {
                    var (success, error) = await _socialLinkService.DeleteSocialLinkAsync(
                        context.Message.UserId, existingLink.SocialNetworkType);

                    if (!success)
                    {
                        _logger.LogWarning("Failed to delete social link {Type}: {Error}", 
                            existingLink.SocialNetworkType, error);
                    }
                }
            }

            await context.RespondAsync(new UpdateSocialLinksResponseContract
            {
                Success = true
            });

            _logger.LogInformation("Successfully updated social links for user {UserId}", context.Message.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating social links for user {UserId}", context.Message.UserId);
            
            await context.RespondAsync(new UpdateSocialLinksResponseContract
            {
                Success = false,
                Error = $"Error updating social links: {ex.Message}"
            });
        }
    }
}

