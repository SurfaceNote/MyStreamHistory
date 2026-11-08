using MyStreamHistory.ViewerService.Domain.Entities;

namespace MyStreamHistory.ViewerService.Application.Interfaces;

public interface IRankingExclusionRepository
{
    Task<List<RankingExcludedAccount>> ListAsync(CancellationToken cancellationToken = default);

    Task<HashSet<string>> GetLoginsAsync(CancellationToken cancellationToken = default);

    /// <summary>Display names of known chat accounts by lower-case login.</summary>
    Task<Dictionary<string, string>> GetDisplayNamesAsync(IReadOnlyCollection<string> logins, CancellationToken cancellationToken = default);

    /// <summary>Returns false when the login is already excluded.</summary>
    Task<bool> AddAsync(string login, string actor, CancellationToken cancellationToken = default);

    /// <summary>Returns false when the login was not excluded.</summary>
    Task<bool> RemoveAsync(string login, CancellationToken cancellationToken = default);

    /// <summary>
    /// Per-channel presence of accounts that are not excluded yet and are not the channel's broadcaster,
    /// limited to channels with at least <paramref name="minChannelMinutes"/> observed minutes and rows
    /// whose presence share is at least <paramref name="minShare"/>.
    /// </summary>
    Task<List<ChannelPresenceRow>> GetHighPresenceRowsAsync(int minChannelMinutes, double minShare, CancellationToken cancellationToken = default);
}

public sealed record ChannelPresenceRow(
    string ViewerTwitchUserId,
    string Login,
    string DisplayName,
    string StreamerTwitchUserId,
    int Minutes,
    int ChannelObservedMinutes,
    decimal MessagePoints,
    int ChannelsWithStats)
{
    public double Share => ChannelObservedMinutes <= 0 ? 0 : (double)Minutes / ChannelObservedMinutes;
}
