namespace MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;

public class GetStreamViewersRequestContract
{
    public Guid StreamSessionId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IncludeSessionPresence { get; set; }
    public List<Guid> StreamCategoryIds { get; set; } = new();

    /// <summary>When set, each viewer also gets <see cref="ViewerCategoryWatchDto.ChannelExperience"/> on this channel.</summary>
    public string? StreamerTwitchUserId { get; set; }
}

