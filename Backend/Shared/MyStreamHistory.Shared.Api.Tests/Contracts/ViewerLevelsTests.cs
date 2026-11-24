using System.Text.Json;
using MyStreamHistory.Shared.Base.Contracts.Viewers;
using Xunit;

namespace MyStreamHistory.Shared.Api.Tests.Contracts;

public class ViewerLevelsTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 7.29)]
    [InlineData(3, 58.32)]
    [InlineData(5, 466.54)]
    [InlineData(10, 5314.19)]
    [InlineData(19, 42513.49)]
    [InlineData(20, 50000)]
    public void Thresholds_follow_cubic_curve(int level, double expected)
    {
        Assert.Equal((decimal)expected, ViewerLevels.ThresholdFor(level));
    }

    [Fact]
    public void Thresholds_strictly_increase()
    {
        for (var level = 2; level <= ViewerLevels.MaxLevel; level++)
        {
            Assert.True(ViewerLevels.ThresholdFor(level) > ViewerLevels.ThresholdFor(level - 1));
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(7.28, 1)]
    [InlineData(7.29, 2)]
    [InlineData(100, 3)]
    [InlineData(49999.99, 19)]
    [InlineData(50000, 20)]
    [InlineData(1000000, 20)]
    public void Level_is_highest_threshold_reached(double experience, int expectedLevel)
    {
        Assert.Equal(expectedLevel, ViewerLevels.ForExperience((decimal)experience).Level);
    }

    [Fact]
    public void Progress_is_share_of_current_level_span()
    {
        var level = ViewerLevels.ForExperience(32.805m); // halfway between 7.29 and 58.32

        Assert.Equal(2, level.Level);
        Assert.Equal(7.29m, level.CurrentLevelExperience);
        Assert.Equal(58.32m, level.NextLevelExperience);
        Assert.Equal(0.5m, level.Progress);
    }

    [Fact]
    public void Max_level_has_no_next_threshold_and_full_progress()
    {
        var level = ViewerLevels.ForExperience(60000m);

        Assert.Null(level.NextLevelExperience);
        Assert.Equal(1m, level.Progress);
    }

    [Fact]
    public void Negative_experience_stays_at_first_level_with_zero_progress()
    {
        var level = ViewerLevels.ForExperience(-5m);

        Assert.Equal(1, level.Level);
        Assert.Equal(0m, level.Progress);
    }

    [Fact]
    public void Stats_dto_serializes_level_and_round_trips()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(new ViewerStatsDto { Experience = 100m }, options);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(3, document.RootElement.GetProperty("level").GetProperty("level").GetInt32());

        var restored = JsonSerializer.Deserialize<ViewerStatsDto>(json, options)!;
        Assert.Equal(100m, restored.Experience);
        Assert.Equal(3, restored.Level.Level);
    }
}
