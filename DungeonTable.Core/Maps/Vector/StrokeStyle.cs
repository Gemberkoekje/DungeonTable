namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// Outline styling for a vector layer or feature. Gate rendering on <see cref="Visible"/>:
/// a Dungeon Scrawl layer often draws only one side (the floor fills but does not stroke;
/// the walls stroke but do not fill).
/// </summary>
public sealed class StrokeStyle
{
    /// <summary>Whether the outline is drawn at all.</summary>
    public bool Visible { get; init; }

    /// <summary>Stroke width in world units (walls are 5, doors/stairs 2).</summary>
    public double Width { get; init; }

    /// <summary>Stroke colour.</summary>
    public Rgba Colour { get; init; }

    /// <summary>Hand-drawn roughening preset.</summary>
    public RoughLevel Rough { get; init; } = RoughLevel.None;
}
