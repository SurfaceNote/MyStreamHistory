using Microsoft.Extensions.Logging.Abstractions;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Application.Services;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Application.Tests.Services;

public class StreamLanguageTests
{
    [Theory]
    [InlineData("ru", "ru")]
    [InlineData(" EN ", "en")]
    [InlineData("other", "other")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    [InlineData("not-a-language-code", null)]
    public void Normalize_ReturnsLowerCaseCodeOrNull(string? input, string? expected)
    {
        Assert.Equal(expected, StreamLanguage.Normalize(input));
    }

    [Fact]
    public async Task Backfill_FillsEachChannelWithItsNormalizedLanguage()
    {
        var filled = new Dictionary<int, string>();
        var repository = Fake<IStreamSessionRepository>.Create(new()
        {
            [nameof(IStreamSessionRepository.GetTwitchUserIdsWithoutLanguageAsync)] =
                _ => Task.FromResult<IReadOnlyList<int>>([1, 2, 3]),
            [nameof(IStreamSessionRepository.FillMissingLanguageAsync)] = args =>
            {
                filled[(int)args[0]!] = (string)args[1]!;
                return Task.FromResult(5);
            }
        });
        IReadOnlyCollection<int>? requested = null;
        var twitch = Fake<ITwitchApiClient>.Create(new()
        {
            [nameof(ITwitchApiClient.GetChannelLanguagesAsync)] = args =>
            {
                requested = (IReadOnlyCollection<int>)args[0]!;
                // Channel 3 is missing from Twitch's answer (deleted or banned); channel 2 sent an empty language.
                return Task.FromResult<IReadOnlyDictionary<int, string>>(
                    new Dictionary<int, string> { [1] = "RU", [2] = " " });
            }
        });

        var updated = await new StreamLanguageBackfillService(
            repository, twitch, NullLogger<StreamLanguageBackfillService>.Instance).BackfillAsync();

        Assert.Equal(new[] { 1, 2, 3 }, requested);
        Assert.Equal(new Dictionary<int, string> { [1] = "ru" }, filled);
        Assert.Equal(5, updated);
    }

    [Fact]
    public async Task Backfill_DoesNotCallTwitchWhenEveryStreamHasALanguage()
    {
        var repository = Fake<IStreamSessionRepository>.Create(new()
        {
            [nameof(IStreamSessionRepository.GetTwitchUserIdsWithoutLanguageAsync)] =
                _ => Task.FromResult<IReadOnlyList<int>>([])
        });
        var twitch = Fake<ITwitchApiClient>.Create(new());

        var updated = await new StreamLanguageBackfillService(
            repository, twitch, NullLogger<StreamLanguageBackfillService>.Instance).BackfillAsync();

        Assert.Equal(0, updated);
    }
}
