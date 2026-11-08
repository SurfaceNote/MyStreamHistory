using MyStreamHistory.Shared.Base.Contracts.Viewers;
using MyStreamHistory.ViewerService.Application.Interfaces;

namespace MyStreamHistory.ViewerService.Application.Services;

/// <summary>
/// Suggests accounts whose chat presence looks automated. A channel's observed time is approximated by
/// the longest presence of any account in its chat (the broadcaster or an always-on bot), because chat
/// presence is only collected while MyStreamHistory tracks the channel.
///
/// An account is a candidate when it was in chat for at least 90% of a channel's observed time,
/// or for at least 60% in three or more channels. Real viewers rarely stay through almost every
/// broadcast; lurker bots and channel bots do. The result is a suggestion for an admin, never applied
/// automatically.
/// </summary>
public static class BotCandidateRules
{
    public const int MinChannelObservedMinutes = 300;
    public const double MinRowShare = 0.6;
    public const double SingleChannelShare = 0.9;
    public const int MultiChannelCount = 3;
    public const int MaxCandidates = 50;

    public static List<BotCandidateDto> Find(IEnumerable<ChannelPresenceRow> rows)
    {
        return rows
            .Where(row => row.ChannelObservedMinutes >= MinChannelObservedMinutes && row.Share >= MinRowShare)
            .GroupBy(row => row.ViewerTwitchUserId)
            .Select(group =>
            {
                var channels = group.OrderByDescending(row => row.Share).ToList();
                var first = channels[0];
                return new
                {
                    Max = first.Share,
                    Candidate = new BotCandidateDto
                    {
                        TwitchUserId = first.ViewerTwitchUserId,
                        Login = first.Login,
                        DisplayName = first.DisplayName,
                        ChannelsCount = first.ChannelsWithStats,
                        HighPresenceChannels = channels.Count,
                        MaxPresenceShare = Math.Round(first.Share, 3),
                        TotalMinutes = channels.Sum(row => row.Minutes),
                        MessagePoints = channels.Sum(row => row.MessagePoints),
                        Channels = channels.Take(5).Select(row => new BotCandidateChannelDto
                        {
                            StreamerTwitchUserId = row.StreamerTwitchUserId,
                            PresenceShare = Math.Round(row.Share, 3),
                            Minutes = row.Minutes
                        }).ToList()
                    }
                };
            })
            .Where(item => item.Max >= SingleChannelShare || item.Candidate.HighPresenceChannels >= MultiChannelCount)
            .OrderByDescending(item => item.Candidate.HighPresenceChannels)
            .ThenByDescending(item => item.Max)
            .ThenByDescending(item => item.Candidate.TotalMinutes)
            .Take(MaxCandidates)
            .Select(item => item.Candidate)
            .ToList();
    }
}
