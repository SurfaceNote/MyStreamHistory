using AutoMapper;
using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class StreamOfflineConsumer : IConsumer<StreamOfflineEventContract>
{
    private readonly IStreamSessionService _streamSessionService;
    private readonly IMapper _mapper;
    private readonly ILogger<StreamOfflineConsumer> _logger;

    public StreamOfflineConsumer(IStreamSessionService streamSessionService, IMapper mapper, ILogger<StreamOfflineConsumer> logger)
    {
        _streamSessionService = streamSessionService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<StreamOfflineEventContract> context)
    {
        _logger.LogInformation("Received stream.offline event for {BroadcasterUserLogin}", context.Message.BroadcasterUserLogin);

        try
        {
            var eventDto = _mapper.Map<StreamOfflineEventDto>(context.Message);
            await _streamSessionService.HandleStreamOfflineAsync(eventDto, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing stream.offline event for {BroadcasterUserLogin}", context.Message.BroadcasterUserLogin);
            throw;
        }
    }
}

