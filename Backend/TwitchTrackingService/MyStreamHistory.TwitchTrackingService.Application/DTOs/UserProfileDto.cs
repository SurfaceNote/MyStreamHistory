namespace MyStreamHistory.TwitchTrackingService.Application.DTOs;

public sealed class UserProfileDto
{
    public string? Login { get; init; }
    public string? DisplayName { get; init; }
    public string? Avatar { get; init; }

    public void ApplyTo(StreamSessionDto session)
    {
        session.StreamerLogin = CurrentOrStored(Login, session.StreamerLogin)!;
        session.StreamerDisplayName = CurrentOrStored(DisplayName, session.StreamerDisplayName)!;
        session.StreamerAvatarUrl = CurrentOrStored(Avatar, session.StreamerAvatarUrl);
    }

    public static string? CurrentOrStored(string? current, string? stored)
        => string.IsNullOrWhiteSpace(current) ? stored : current;
}
