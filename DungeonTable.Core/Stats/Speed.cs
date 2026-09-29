namespace DungeonTable.Core.Stats;

/// <summary>One of a creature's movement modes ("fly 30 ft. (hover)").</summary>
public sealed class Speed
{
    /// <summary>Which movement mode this is.</summary>
    public SpeedKind Kind { get; init; } = SpeedKind.None;

    /// <summary>Speed in feet.</summary>
    public int Feet { get; init; }

    /// <summary>A trailing qualifier printed after the speed ("hover"), or empty when none.</summary>
    public string Note { get; init; } = string.Empty;
}
