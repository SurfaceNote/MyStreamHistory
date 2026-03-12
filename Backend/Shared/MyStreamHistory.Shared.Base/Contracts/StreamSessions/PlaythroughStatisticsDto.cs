namespace MyStreamHistory.Shared.Base.Contracts.StreamSessions;

public class PlaythroughStatisticsDto
{
    public Guid PlaythroughId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid TwitchCategoryId { get; set; }
    public string TwitchCategoryTwitchId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string BoxArtUrl { get; set; } = string.Empty;
    public DateTime? FirstStreamStartedAt { get; set; }
    public DateTime? LastStreamStartedAt { get; set; }
    public int UniqueViewersCount { get; set; }
    public double TotalHours { get; set; }
}
