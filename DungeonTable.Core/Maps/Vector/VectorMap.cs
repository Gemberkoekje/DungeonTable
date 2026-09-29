namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// A fully-vector dungeon map reconstructed from a Dungeon Scrawl <c>.ds</c> file: the
/// ordered template layer stack (floor, walls, effects, grid), the door/stairs object
/// layer, plus calibration (<see cref="Bounds"/> and <see cref="Grid"/>). This is the
/// single source of truth the DM and player views both render, filtered differently.
/// Room partitioning and object behaviour are authored separately (they are not in the
/// source file).
/// </summary>
public sealed class VectorMap
{
    /// <summary>Stable map identifier (for example <c>level-1-undercroft</c>).</summary>
    public string MapId { get; init; } = string.Empty;

    /// <summary>Adventure this map belongs to (for example <c>demo</c>).</summary>
    public string Adventure { get; init; } = string.Empty;

    /// <summary>Human-readable level name; supplied externally (not in the source file).</summary>
    public string LevelName { get; init; } = string.Empty;

    /// <summary>Bounding box of all geometry, used for the SVG viewBox.</summary>
    public MapBounds Bounds { get; init; }

    /// <summary>Grid calibration derived from the source cell size and geometry bounds.</summary>
    public Grid Grid { get; init; } = new Grid();

    /// <summary>Template layer stack in z-order (bottom first).</summary>
    public IReadOnlyList<MapLayer> Layers { get; init; } = Array.Empty<MapLayer>();

    /// <summary>Door objects.</summary>
    public IReadOnlyList<DoorFeature> Doors { get; init; } = Array.Empty<DoorFeature>();

    /// <summary>Stair objects.</summary>
    public IReadOnlyList<StairFeature> Stairs { get; init; } = Array.Empty<StairFeature>();

    /// <summary>Decoration sprites (statues, pillars, rubble, …), placed by transform.</summary>
    public IReadOnlyList<MapDecoration> Decorations { get; init; } = Array.Empty<MapDecoration>();

    /// <summary>The distinct sprites referenced by <see cref="Decorations"/>, stored once each.</summary>
    public IReadOnlyList<SpriteImage> Sprites { get; init; } = Array.Empty<SpriteImage>();
}
