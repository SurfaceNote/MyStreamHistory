using MyStreamHistory.Shared.Base.Contracts.Viewers;
using Xunit;

namespace MyStreamHistory.Gateway.Application.Tests;

public class RankingExclusionsTests
{
    [Theory]
    [InlineData("nightbot", "nightbot")]
    [InlineData(" StreamElements ", "streamelements")]
    [InlineData("@Kination_Bot", "kination_bot")]
    [InlineData("a", "a")]
    [InlineData("abcdefghijklmnopqrstuvwxy", "abcdefghijklmnopqrstuvwxy")]
    public void ValidLogins_AreNormalizedToLowerCase(string input, string expected) =>
        Assert.Equal(expected, RankingExclusions.NormalizeLogin(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("night-bot")]
    [InlineData("../streams")]
    [InlineData("бот")]
    [InlineData("abcdefghijklmnopqrstuvwxyz")]
    public void InvalidLogins_AreRejected(string? input) =>
        Assert.Null(RankingExclusions.NormalizeLogin(input));

    [Fact]
    public void Broadcaster_IsRecognisedOnlyOnOwnChannel()
    {
        Assert.True(RankingExclusions.IsBroadcaster("123", "123"));
        Assert.False(RankingExclusions.IsBroadcaster("123", "456"));
        Assert.False(RankingExclusions.IsBroadcaster(null, "123"));
    }
}
