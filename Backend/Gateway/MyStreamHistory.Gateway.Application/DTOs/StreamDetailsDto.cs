using MyStreamHistory.Shared.Base.Contracts.StreamSessions;
using MyStreamHistory.Shared.Base.Contracts.Viewers;

namespace MyStreamHistory.Gateway.Application.DTOs;

public class StreamDetailsDto
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
    
    public List<CategoryDetailsDto> Categories { get; set; } = new();
    public List<StreamViewerDto> Viewers { get; set; } = new();
}

public class CategoryDetailsDto
{
    public Guid StreamCategoryId { get; set; }
    public string TwitchId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string BoxArtUrl { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int DurationMinutes { get; set; }
    public int UniqueViewers { get; set; }
}

public class StreamViewerDto
{
    public string TwitchUserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public int MinutesWatched { get; set; }
    public decimal ChatPoints { get; set; }
    public decimal ViewerPoints { get; set; }
}

