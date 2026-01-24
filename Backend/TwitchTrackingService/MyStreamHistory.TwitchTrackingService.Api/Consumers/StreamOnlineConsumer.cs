using AutoMapper;
using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class StreamOnlineConsumer : IConsumer<StreamOnlineEventContract>
{
    private readonly IStreamSessionService _streamSessionService;
    private readonly IMapper _mapper;
    private readonly ILogger<StreamOnlineConsumer> _logger;

    public StreamOnlineConsumer(IStreamSessionService streamSessionService, IMapper mapper, ILogger<StreamOnlineConsumer> logger)
    {
        _streamSessionService = streamSessionService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<StreamOnlineEventContract> context)
    {
        _logger.LogInformation("Received stream.online event for {BroadcasterUserLogin}", context.Message.BroadcasterUserLogin);

        try
        {
            var eventDto = _mapper.Map<StreamOnlineEventDto>(context.Message);
            await _streamSessionService.HandleStreamOnlineAsync(eventDto, context.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing stream.online event for {BroadcasterUserLogin}", context.Message.BroadcasterUserLogin);
            throw;
        }
    }
}

