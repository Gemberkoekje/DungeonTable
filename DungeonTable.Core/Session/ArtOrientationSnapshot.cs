namespace DungeonTable.Core.Session;

/// <summary>How one picture is turned and flipped on the projector, as it is saved.</summary>
public sealed class ArtOrientationSnapshot
{
    /// <summary>The picture's catalogue id.</summary>
    public string ImageId { get; set; } = string.Empty;

    /// <summary>Quarter turns clockwise, from 0 to 3.</summary>
    public int QuarterTurns { get; set; }

    /// <summary>True when it is flipped left to right.</summary>
    public bool Mirrored { get; set; }
}
