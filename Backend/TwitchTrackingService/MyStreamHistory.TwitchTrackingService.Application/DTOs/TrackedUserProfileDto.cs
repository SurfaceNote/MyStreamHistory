namespace MyStreamHistory.TwitchTrackingService.Application.DTOs;

public sealed class TrackedUserProfileDto
{
    public int TwitchUserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
}
