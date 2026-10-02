namespace MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;

public class GetLiveStreamersResponseContract
{
    public List<int> TwitchUserIds { get; set; } = new();
}
