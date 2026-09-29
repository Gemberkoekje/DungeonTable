namespace DungeonTable.Core.Maps;

/// <summary>
/// A single dungeon level's editor annotations over a fully-vector map: keyed region polygons,
/// feature markers, and per-object state. Persisted as a <c>{mapId}.regions.json</c> document
/// authored in the Room Editor. Geometry and the grid live in the sibling Dungeon Scrawl
/// <c>.ds</c> (rendered into a <see cref="Vector.VectorMap"/>); this document holds annotations
/// only and is keyed to that map by <see cref="MapId"/>. All coordinates are in <c>.ds</c> world
/// units so they overlay the vector renderer directly.
/// </summary>
public sealed class MapDefinition
{
    /// <summary>Stable map identifier; matches the vector map id (the <c>.ds</c> file stem).</summary>
    public string MapId { get; init; } = string.Empty;

    /// <summary>Adventure this map belongs to (e.g. "demo").</summary>
    public string Adventure { get; init; } = string.Empty;

    /// <summary>Human-readable level name.</summary>
    public string LevelName { get; init; } = string.Empty;

    /// <summary>Keyed regions drawn on this map, in world units.</summary>
    public IReadOnlyList<Region> Regions { get; init; } = Array.Empty<Region>();

    /// <summary>Feature markers placed on this map, in world units.</summary>
    public IReadOnlyList<FeatureMarker> Features { get; init; } = Array.Empty<FeatureMarker>();

    /// <summary>Editor-owned state for imported door and stair objects, keyed by object id.</summary>
    public IReadOnlyList<ObjectAnnotation> Objects { get; init; } = Array.Empty<ObjectAnnotation>();
}
