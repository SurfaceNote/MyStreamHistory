namespace MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;

public class GetRecentStreamsRequestContract
{
    public int TwitchUserId { get; set; }
    public int Count { get; set; } = 10;
}

