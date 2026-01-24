namespace MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;

public class GetRecentStreamsResponseContract
{
    public List<StreamSessionDto> StreamSessions { get; set; } = new();
}

