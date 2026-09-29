namespace DungeonTable.Core.Maps;

/// <summary>
/// The kind of imported object an <see cref="ObjectAnnotation"/> is keyed to. Mirrors the object
/// kinds a Dungeon Scrawl <c>.ds</c> carries; the geometry itself lives in the vector map, so this
/// only records which imported object an annotation belongs to.
/// </summary>
public enum MapObjectKind
{
    /// <summary>Unknown / unset.</summary>
    None = 0,

    /// <summary>A door object.</summary>
    Door = 1,

    /// <summary>A stair object.</summary>
    Stairs = 2,

    /// <summary>A placed decoration sprite (statue, pillar, rubble, pit, …).</summary>
    Decoration = 3,
}
