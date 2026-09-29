namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// A structural door in the object layer: pure geometry from the source file (a leaf
/// polygon plus jamb-stub polylines, already in world coordinates). Interactive state —
/// open/closed, whether the door is secret, and per-session reveal — is <b>not</b> in the
/// source file and is deliberately not modelled here; it lives in the editor/session
/// annotations keyed by <see cref="Id"/>.
/// </summary>
public sealed class DoorFeature
{
    /// <summary>Source object id (the <c>DUNGEON_ASSET</c> node id).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Door geometry in world units.</summary>
    public VectorGeometry Geometry { get; init; } = VectorGeometry.Empty;

    /// <summary>Orientation derived from the leaf bounding box.</summary>
    public DoorOrientation Orientation { get; init; } = DoorOrientation.None;

    /// <summary>Bounding box of the door geometry.</summary>
    public MapBounds Bounds { get; init; }
}
