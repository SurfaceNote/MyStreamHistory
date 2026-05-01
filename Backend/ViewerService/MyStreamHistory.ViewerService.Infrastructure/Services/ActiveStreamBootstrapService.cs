using Microsoft.Extensions.Logging;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.Users.Requests;
using MyStreamHistory.Shared.Base.Contracts.Users.Response;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Infrastructure.Services;

public class ActiveStreamBootstrapService : IActiveStreamBootstrapService
{
    private readonly ITransportBus _transportBus;
    private readonly IStreamCategoryService _streamCategoryService;
    private readonly IViewerTrackingService _viewerTrackingService;
    private readonly IChatMessageBufferService _bufferService;
    private readonly ILogger<ActiveStreamBootstrapService> _logger;

    public ActiveStreamBootstrapService(
        ITransportBus transportBus,
        IStreamCategoryService streamCategoryService,
        IViewerTrackingService viewerTrackingService,
        IChatMessageBufferService bufferService,
        ILogger<ActiveStreamBootstrapService> logger)
    {
        _transportBus = transportBus;
        _streamCategoryService = streamCategoryService;
        _viewerTrackingService = viewerTrackingService;
        _bufferService = bufferService;
        _logger = logger;
    }

    public async Task BootstrapActiveStreamsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Bootstrapping active viewer streams");

        var response = await _transportBus.SendRequestAsync<GetAllUsersRequestContract, GetAllUsersResponseContract>(
            new GetAllUsersRequestContract(),
            cancellationToken);

        var users = response.Success?.Users;
        if (users == null || users.Count == 0)
        {
            _logger.LogWarning("No users returned from AuthService during active stream bootstrap");
            return;
        }

        var bootstrappedCount = 0;
        foreach (var user in users)
        {
            var twitchUserId = user.TwitchId.ToString();
            if (_bufferService.IsStreamActive(twitchUserId))
            {
                continue;
            }

            var activeStream = await _streamCategoryService.GetActiveStreamCategoryAsync(twitchUserId, cancellationToken);
            if (activeStream == null)
            {
                continue;
            }

            await _viewerTrackingService.HandleStreamOnlineAsync(
                twitchUserId,
                activeStream.Value.StreamSessionId,
                activeStream.Value.StreamCategoryId,
                cancellationToken);

            bootstrappedCount++;
        }

        _logger.LogInformation("Active viewer stream bootstrap completed. Bootstrapped {BootstrappedCount} streams, active buffers: {ActiveBufferCount}",
            bootstrappedCount, _bufferService.GetActiveStreamCount());
    }
}
