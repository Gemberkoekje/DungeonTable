using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// A door, stair or decoration object to outline in the editor overlay: its source id and bounds
/// plus the editor state that drives the outline style (selected, hidden from the players, or
/// linked to a node). The geometry itself is drawn by the base object pass; this only adds the editor
/// annotation outline.
/// </summary>
public sealed class ObjectHighlight
{
    /// <summary>Source object id (matches the base object pass's <c>data-object-id</c>).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Whether the object is a door, a stair or a decoration.</summary>
    public MapObjectKind Kind { get; init; } = MapObjectKind.None;

    /// <summary>Object bounds in world units.</summary>
    public MapBounds Bounds { get; init; }

    /// <summary>True when this is a door marked secret.</summary>
    public bool IsSecret { get; init; }

    /// <summary>True when this object is marked concealed (hidden from the players until revealed).</summary>
    public bool IsConcealed { get; init; }

    /// <summary>True when this object is linked to a node.</summary>
    public bool Linked { get; init; }
}
