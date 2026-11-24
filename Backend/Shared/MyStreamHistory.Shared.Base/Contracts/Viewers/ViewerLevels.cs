namespace MyStreamHistory.Shared.Base.Contracts.Viewers;

/// <summary>
/// Viewer levels on a channel. Levels are derived from channel experience and are never stored,
/// so the curve can be tuned without migrations. Threshold for level L is
/// <c>MaxExperience · ((L − 1) / (MaxLevel − 1))³</c>, rounded to two decimals.
/// </summary>
public static class ViewerLevels
{
    public const int MaxLevel = 20;
    public const decimal MaxExperience = 50000m;

    private static readonly decimal[] Thresholds = BuildThresholds();

    /// <summary>Experience needed to reach <paramref name="level"/> (1-based).</summary>
    public static decimal ThresholdFor(int level)
    {
        if (level < 1 || level > MaxLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, $"Level must be between 1 and {MaxLevel}.");
        }

        return Thresholds[level - 1];
    }

    public static ViewerLevelDto ForExperience(decimal experience)
    {
        var level = 1;
        while (level < MaxLevel && experience >= Thresholds[level])
        {
            level++;
        }

        var current = Thresholds[level - 1];
        decimal? next = level < MaxLevel ? Thresholds[level] : null;
        var progress = next is { } nextThreshold
            ? Math.Clamp((experience - current) / (nextThreshold - current), 0m, 1m)
            : 1m;

        return new ViewerLevelDto
        {
            Level = level,
            CurrentLevelExperience = current,
            NextLevelExperience = next,
            Progress = Math.Round(progress, 4)
        };
    }

    private static decimal[] BuildThresholds()
    {
        var thresholds = new decimal[MaxLevel];
        decimal steps = MaxLevel - 1;
        for (var i = 0; i < MaxLevel; i++)
        {
            thresholds[i] = Math.Round(MaxExperience * i * i * i / (steps * steps * steps), 2);
        }

        return thresholds;
    }
}

public class ViewerLevelDto
{
    /// <summary>Level from 1 to <see cref="ViewerLevels.MaxLevel"/>.</summary>
    public int Level { get; init; }

    /// <summary>Experience at which the current level starts.</summary>
    public decimal CurrentLevelExperience { get; init; }

    /// <summary>Experience at which the next level starts; null at the maximum level.</summary>
    public decimal? NextLevelExperience { get; init; }

    /// <summary>Progress towards the next level, from 0 to 1 (1 at the maximum level).</summary>
    public decimal Progress { get; init; }
}
