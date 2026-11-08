namespace MyStreamHistory.Shared.Base.Contracts.Viewers;

/// <summary>
/// Accounts that are present in chat but are not part of a channel's audience: the broadcaster
/// and chat bots. They stay in raw chat statistics but are excluded from viewer rankings,
/// the overlay and rank badges. The bot list itself is stored in ViewerService
/// (table RankingExcludedAccounts, seeded with well-known bots) and managed in the admin panel.
/// </summary>
public static class RankingExclusions
{
    /// <summary>Twitch logins: 1–25 characters, letters, digits and underscores (stored in lower case).</summary>
    public static string? NormalizeLogin(string? login)
    {
        var value = login?.Trim().TrimStart('@').ToLowerInvariant();
        return value is { Length: >= 1 and <= 25 } && value.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_')
            ? value
            : null;
    }

    public static bool IsBroadcaster(string? viewerTwitchUserId, string? streamerTwitchUserId) =>
        !string.IsNullOrWhiteSpace(viewerTwitchUserId)
        && string.Equals(viewerTwitchUserId.Trim(), streamerTwitchUserId?.Trim(), StringComparison.Ordinal);
}

public class GetRankingExclusionsRequest
{
    public bool IncludeCandidates { get; init; } = true;
}

public class GetRankingExclusionsResponse
{
    public List<RankingExcludedAccountDto> Accounts { get; init; } = [];
    public List<BotCandidateDto> Candidates { get; init; } = [];
}

public class RankingExcludedAccountDto
{
    public string Login { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string AddedBy { get; init; } = string.Empty;
    public DateTime AddedAt { get; init; }
}

/// <summary>An account whose chat presence looks automated. Only a suggestion: an admin decides.</summary>
public class BotCandidateDto
{
    public string TwitchUserId { get; init; } = string.Empty;
    public string Login { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int ChannelsCount { get; init; }
    public int HighPresenceChannels { get; init; }
    public double MaxPresenceShare { get; init; }
    public int TotalMinutes { get; init; }
    public decimal MessagePoints { get; init; }
    public List<BotCandidateChannelDto> Channels { get; init; } = [];
}

public class BotCandidateChannelDto
{
    public string StreamerTwitchUserId { get; init; } = string.Empty;
    public string? StreamerDisplayName { get; set; }
    public double PresenceShare { get; init; }
    public int Minutes { get; init; }
}

public class ChangeRankingExclusionRequest
{
    public string Login { get; init; } = string.Empty;
    public string Actor { get; init; } = string.Empty;
    public bool Exclude { get; init; }
}

public class ChangeRankingExclusionResponse
{
    public bool Success { get; init; }
    public string? Error { get; init; }
}
