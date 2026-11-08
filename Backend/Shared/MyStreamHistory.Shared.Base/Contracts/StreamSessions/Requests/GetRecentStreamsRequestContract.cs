namespace MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;

public class GetRecentStreamsRequestContract
{
    public int TwitchUserId { get; set; }
    public int Count { get; set; } = 10;

    /// <summary>Optional cursor: only streams that started before this UTC time.</summary>
    public DateTime? Before { get; set; }
}

