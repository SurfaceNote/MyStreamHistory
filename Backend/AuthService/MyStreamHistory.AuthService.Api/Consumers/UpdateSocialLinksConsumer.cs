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
        _logger.LogInformation("Received request to update social link for user {UserId}, type: {Type}", 
            context.Message.UserId, context.Message.SocialLink.SocialNetworkType);

        try
        {
            var linkDto = context.Message.SocialLink;
            
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

            var existingLinks = await _socialLinkService.GetSocialLinksByUserIdAsync(context.Message.UserId);
            var existingLink = existingLinks.FirstOrDefault(el => el.SocialNetworkType == type);

            // Если путь пустой или null, это запрос на удаление
            if (string.IsNullOrWhiteSpace(linkDto.Path))
            {
                if (existingLink != null)
                {
                    var (success, error) = await _socialLinkService.DeleteSocialLinkAsync(
                        context.Message.UserId, type);

                    if (!success)
                    {
                        _logger.LogWarning("Failed to delete social link {Type}: {Error}", type, error);
                        await context.RespondAsync(new UpdateSocialLinksResponseContract
                        {
                            Success = false,
                            Error = error
                        });
                        return;
                    }
                    
                    _logger.LogInformation("Successfully deleted social link for user {UserId}, type: {Type}", 
                        context.Message.UserId, type);
                }
                else
                {
                    // Ссылка уже не существует, считаем успехом
                    _logger.LogInformation("Social link {Type} already doesn't exist for user {UserId}", 
                        type, context.Message.UserId);
                }
            }
            else if (existingLink != null)
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
                
                _logger.LogInformation("Successfully updated social link for user {UserId}, type: {Type}", 
                    context.Message.UserId, type);
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
                
                _logger.LogInformation("Successfully added social link for user {UserId}, type: {Type}", 
                    context.Message.UserId, type);
            }

            await context.RespondAsync(new UpdateSocialLinksResponseContract
            {
                Success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating social link for user {UserId}", context.Message.UserId);
            
            await context.RespondAsync(new UpdateSocialLinksResponseContract
            {
                Success = false,
                Error = $"Error updating social link: {ex.Message}"
            });
        }
    }
}
