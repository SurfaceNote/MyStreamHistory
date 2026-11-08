namespace MyStreamHistory.Shared.Base.Contracts.StreamSessions;

public class GetAdminStreamsRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class GetAdminStreamsResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<AdminStreamDto> Items { get; set; } = [];
}

public class AdminStreamDto
{
    public Guid Id { get; set; }
    public string StreamerLogin { get; set; } = "";
    public string StreamerDisplayName { get; set; } = "";
    public string? StreamerAvatarUrl { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsLive { get; set; }
    public double? DurationHours { get; set; }
    public List<string> Categories { get; set; } = [];
    public List<Guid> CategoryIds { get; set; } = [];
    public int? UniqueViewers { get; set; }
}

public class GetAdminStreamViewerCountsRequest
{
    public List<AdminStreamViewerScope> Streams { get; set; } = [];
}

public class AdminStreamViewerScope
{
    public Guid StreamSessionId { get; set; }
    public List<Guid> CategoryIds { get; set; } = [];
}

public class GetAdminStreamViewerCountsResponse
{
    public Dictionary<Guid, int> Counts { get; set; } = [];
}
