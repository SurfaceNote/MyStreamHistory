using AutoMapper;
using MyStreamHistory.Gateway.Application.Commands;
using MyStreamHistory.Gateway.Application.DTOs;
using MyStreamHistory.Shared.Api.DTOs;

namespace MyStreamHistory.Gateway.Api.Mapping;

public class ApiToApplicationProfile : Profile
{
    public ApiToApplicationProfile()
    {
        CreateMap<TwitchCallbackRequest, TwitchCallbackCommand>();
        CreateMap<RefreshTokenRequest, RefreshTokenCommand>();

        CreateMap<TwitchCallbackResultDto, TwitchCallbackResponse>();
        CreateMap<RefreshTokenResultDto, RefreshTokenResponse>();
    }
}