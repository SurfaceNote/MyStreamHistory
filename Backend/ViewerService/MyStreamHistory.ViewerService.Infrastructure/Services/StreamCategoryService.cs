using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Infrastructure.Services;

public class StreamCategoryService : IStreamCategoryService
{
    private readonly ITransportBus _transportBus;
    private readonly ILogger<StreamCategoryService> _logger;

    public StreamCategoryService(
        ITransportBus transportBus,
        ILogger<StreamCategoryService> logger)
    {
        _transportBus = transportBus;
        _logger = logger;
    }

    public async Task<(Guid StreamSessionId, Guid? StreamCategoryId)?> GetActiveStreamCategoryAsync(string twitchUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _transportBus.SendRequestAsync<
                GetActiveStreamCategoryRequestContract,
                GetActiveStreamCategoryResponseContract>(
                new GetActiveStreamCategoryRequestContract
                {
                    TwitchUserId = twitchUserId
                },
                cancellationToken);

            var result = response.Success;
            
            if (result == null || !result.Success || result.StreamSessionId == null)
            {
                _logger.LogWarning("Failed to get active stream category for TwitchUserId: {TwitchUserId}, Error: {Error}", 
                    twitchUserId, result?.Error);
                return null;
            }

            return (result.StreamSessionId.Value, result.StreamCategoryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while getting active stream category for TwitchUserId: {TwitchUserId}", twitchUserId);
            return null;
        }
    }
}

