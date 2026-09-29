namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// Fill styling for a vector layer or feature. As with <see cref="StrokeStyle"/>, gate
/// rendering on <see cref="Visible"/> (walls and masks fill nothing).
/// </summary>
public sealed class FillStyle
{
    /// <summary>Whether the interior is filled at all.</summary>
    public bool Visible { get; init; }

    /// <summary>Fill colour.</summary>
    public Rgba Colour { get; init; }
}
