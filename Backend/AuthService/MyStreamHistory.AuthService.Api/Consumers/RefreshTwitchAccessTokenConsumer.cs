using System.Net.Http.Json;
using MassTransit;
using Microsoft.Extensions.Options;
using MyStreamHistory.AuthService.Application.DTOs.Twitch;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Infrastructure.Options;
using MyStreamHistory.Shared.Application.UnitOfWork;
using MyStreamHistory.Shared.Base.Contracts.Auth.Requests;
using MyStreamHistory.Shared.Base.Contracts.Auth.Responses;

namespace MyStreamHistory.AuthService.Api.Consumers;

public class RefreshTwitchAccessTokenConsumer : IConsumer<RefreshTwitchAccessTokenRequestContract>
{
    private readonly IAuthUserRepository _authUserRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HttpClient _httpClient;
    private readonly TwitchOptions _twitchOptions;
    private readonly ILogger<RefreshTwitchAccessTokenConsumer> _logger;

    public RefreshTwitchAccessTokenConsumer(
        IAuthUserRepository authUserRepository,
        IUnitOfWork unitOfWork,
        HttpClient httpClient,
        IOptions<TwitchOptions> twitchOptions,
        ILogger<RefreshTwitchAccessTokenConsumer> logger)
    {
        _authUserRepository = authUserRepository;
        _unitOfWork = unitOfWork;
        _httpClient = httpClient;
        _twitchOptions = twitchOptions.Value;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RefreshTwitchAccessTokenRequestContract> context)
    {
        var request = context.Message;
        
        _logger.LogInformation("Token refresh request from {RequestingService} for TwitchUserId: {TwitchUserId}", 
            request.RequestingService, request.TwitchUserId);

        try
        {
            // Parse TwitchUserId to int
            if (!int.TryParse(request.TwitchUserId, out var twitchUserId))
            {
                _logger.LogWarning("Invalid TwitchUserId format: {TwitchUserId}", request.TwitchUserId);
                
                await context.RespondAsync(new RefreshTwitchAccessTokenResponseContract
                {
                    Success = false,
                    Error = $"Invalid TwitchUserId format: {request.TwitchUserId}"
                });
                return;
            }

            // Get user
            var user = await _authUserRepository.GetUserByTwitchIdAsync(twitchUserId);
            
            if (user == null)
            {
                _logger.LogWarning("User not found for TwitchUserId: {TwitchUserId}", twitchUserId);
                
                await context.RespondAsync(new RefreshTwitchAccessTokenResponseContract
                {
                    Success = false,
                    Error = $"User not found for TwitchUserId: {twitchUserId}"
                });
                return;
            }

            if (string.IsNullOrEmpty(user.TwitchRefreshToken))
            {
                _logger.LogWarning("No Twitch refresh token for TwitchUserId: {TwitchUserId}", twitchUserId);
                
                await context.RespondAsync(new RefreshTwitchAccessTokenResponseContract
                {
                    Success = false,
                    Error = $"No Twitch refresh token for TwitchUserId: {twitchUserId}"
                });
                return;
            }

            // Refresh token via Twitch API
            var requestContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _twitchOptions.ClientId),
                new KeyValuePair<string, string>("client_secret", _twitchOptions.ClientSecret),
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", user.TwitchRefreshToken)
            });

            using var response = await _httpClient.PostAsync(_twitchOptions.TokenEndpoint, requestContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to refresh Twitch token for TwitchUserId: {TwitchUserId}. Status: {StatusCode}, Error: {Error}", 
                    twitchUserId, response.StatusCode, errorContent);
                
                await context.RespondAsync(new RefreshTwitchAccessTokenResponseContract
                {
                    Success = false,
                    Error = $"Twitch API error: {response.StatusCode}"
                });
                return;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponseDto>();
            
            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                _logger.LogError("Invalid token response from Twitch for TwitchUserId: {TwitchUserId}", twitchUserId);
                
                await context.RespondAsync(new RefreshTwitchAccessTokenResponseContract
                {
                    Success = false,
                    Error = "Invalid token response from Twitch"
                });
                return;
            }

            // Update user tokens
            user.TwitchAccessToken = tokenResponse.AccessToken;
            if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
            {
                user.TwitchRefreshToken = tokenResponse.RefreshToken;
            }
            user.IsTwitchTokenFresh = true;

            await _authUserRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync();

            await context.RespondAsync(new RefreshTwitchAccessTokenResponseContract
            {
                Success = true,
                AccessToken = tokenResponse.AccessToken,
                ExpiresAt = DateTime.UtcNow.AddHours(4) // Twitch tokens typically last ~4 hours
            });

            _logger.LogInformation("Successfully refreshed token for TwitchUserId: {TwitchUserId}", twitchUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Twitch access token for TwitchUserId: {TwitchUserId}", 
                request.TwitchUserId);
            
            await context.RespondAsync(new RefreshTwitchAccessTokenResponseContract
            {
                Success = false,
                Error = $"Internal error: {ex.Message}"
            });
        }
    }
}

