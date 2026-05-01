namespace MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;

public class GetStreamerStatisticsRequestContract
{
    public int TwitchUserId { get; set; }
    public string Period { get; set; } = "30d";
}

