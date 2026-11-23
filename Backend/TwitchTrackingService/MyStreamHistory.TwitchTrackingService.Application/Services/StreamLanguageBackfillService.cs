using Microsoft.Extensions.Logging;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;

namespace MyStreamHistory.TwitchTrackingService.Application.Services;

/// <summary>
/// Fills <c>StreamSession.Language</c> for ended streams recorded before the language was stored,
/// using each channel's current broadcaster language. Idempotent: once every ended stream has a
/// language there is nothing left to do, so it is safe to run on every start.
/// </summary>
public interface IStreamLanguageBackfillService
{
    /// <summary>Returns the number of stream sessions that got a language.</summary>
    Task<int> BackfillAsync(CancellationToken cancellationToken = default);
}

public class StreamLanguageBackfillService(
    IStreamSessionRepository repository,
    ITwitchApiClient twitchApiClient,
    ILogger<StreamLanguageBackfillService> logger) : IStreamLanguageBackfillService
{
    public async Task<int> BackfillAsync(CancellationToken cancellationToken = default)
    {
        var userIds = await repository.GetTwitchUserIdsWithoutLanguageAsync(cancellationToken);
        if (userIds.Count == 0)
        {
            logger.LogDebug("Stream language backfill: nothing to fill");
            return 0;
        }

        var channelLanguages = await twitchApiClient.GetChannelLanguagesAsync(userIds, cancellationToken);
        var updated = 0;
        foreach (var userId in userIds)
        {
            var language = StreamLanguage.Normalize(channelLanguages.GetValueOrDefault(userId));
            if (language == null)
            {
                continue;
            }

            updated += await repository.FillMissingLanguageAsync(userId, language, cancellationToken);
        }

        logger.LogInformation(
            "Stream language backfill: {UpdatedCount} streams filled for {ChannelCount} of {TotalCount} channels",
            updated,
            channelLanguages.Count,
            userIds.Count);
        return updated;
    }
}
