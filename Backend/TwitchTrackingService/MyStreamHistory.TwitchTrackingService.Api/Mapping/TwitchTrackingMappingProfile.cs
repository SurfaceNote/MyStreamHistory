using AutoMapper;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.TwitchTrackingService.Application.DTOs;

namespace MyStreamHistory.TwitchTrackingService.Api.Mapping;

public class TwitchTrackingMappingProfile : Profile
{
    public TwitchTrackingMappingProfile()
    {
        CreateMap<StreamOnlineEventContract, StreamOnlineEventDto>();
        CreateMap<StreamOfflineEventContract, StreamOfflineEventDto>();
    }
}

