using DungeonTable.Core.Maps.Vector;

namespace DungeonTable.Core.Maps;

/// <summary>
/// Editor-owned state for one imported door, stair or decoration object, keyed by its source object
/// id (<see cref="DoorFeature.Id"/> / <see cref="StairFeature.Id"/> /
/// <see cref="MapDecoration.Id"/>). The <c>.ds</c> supplies only the object's geometry and kind;
/// whether a door is secret, whether an object starts concealed, its default open/closed state, and
/// any node link are authored in the Room Editor and toggled per session — never baked from the
/// source file.
/// </summary>
public sealed class ObjectAnnotation
{
    /// <summary>Source object id this annotation is keyed to.</summary>
    public string ObjectId { get; init; } = string.Empty;

    /// <summary>Whether the annotated object is a door, a stair or a decoration.</summary>
    public MapObjectKind Kind { get; init; } = MapObjectKind.None;

    /// <summary>Human-readable label (for example <c>"Iron door"</c>).</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// True when this door is secret: hidden on the player view until individually revealed.
    /// Not meaningful for stairs.
    /// </summary>
    public bool IsSecret { get; init; }

    /// <summary>
    /// True when this object starts concealed: the players never see it until the DM reveals it for
    /// the session, and the DM's own map draws it ghosted while it is still hidden. Authored for
    /// stairs and decorations (a covered pit, a statue that is not there yet, a hidden staircase); a
    /// door uses <see cref="IsSecret"/> instead, which renders it as wall rather than removing it.
    /// </summary>
    public bool IsConcealed { get; init; }

    /// <summary>
    /// The door's default open state at the start of a session. Not meaningful for stairs.
    /// </summary>
    public bool DefaultOpen { get; init; }

    /// <summary>
    /// True when this door is a double door: it is drawn as a pair of leaves (open or closed)
    /// instead of a single leaf. Presentation only; not meaningful for stairs.
    /// </summary>
    public bool DoubleDoors { get; init; }

    /// <summary>Linked node id, or an empty string when unlinked.</summary>
    public string GraphNodeId { get; init; } = string.Empty;
}
