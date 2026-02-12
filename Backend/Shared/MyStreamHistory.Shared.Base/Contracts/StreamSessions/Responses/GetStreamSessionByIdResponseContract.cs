namespace MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;

public class GetStreamSessionByIdResponseContract
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public StreamSessionDetailsDto? StreamSession { get; set; }
}

public class StreamSessionDetailsDto
{
    public Guid Id { get; set; }
    public string? StreamId { get; set; }
    public int TwitchUserId { get; set; }
    public string StreamerLogin { get; set; } = null!;
    public string StreamerDisplayName { get; set; } = null!;
    public string? StreamerAvatarUrl { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsLive { get; set; }
    public string? StreamTitle { get; set; }
    public string? GameName { get; set; }
    public int? ViewerCount { get; set; }
    
    public List<StreamCategoryDetailsDto> Categories { get; set; } = new();
}

public class StreamCategoryDetailsDto
{
    public Guid StreamCategoryId { get; set; }
    public string TwitchCategoryId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string BoxArtUrl { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int DurationMinutes { get; set; }
}

