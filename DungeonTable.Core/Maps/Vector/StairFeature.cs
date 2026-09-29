namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// A structural stair in the object layer: step-line polylines from the source file (no
/// polygons). Some stair blobs are entirely empty and render nothing. As with
/// <see cref="DoorFeature"/>, no interactive state is modelled here.
/// </summary>
public sealed class StairFeature
{
    /// <summary>Source object id (the <c>DUNGEON_ASSET</c> node id).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Stair geometry (step lines) in world units; may be empty.</summary>
    public VectorGeometry Geometry { get; init; } = VectorGeometry.Empty;

    /// <summary>Ascent direction; left <see cref="StairDirection.None"/> by the reader.</summary>
    public StairDirection Direction { get; init; } = StairDirection.None;

    /// <summary>Bounding box of the stair geometry; empty when the geometry is empty.</summary>
    public MapBounds Bounds { get; init; }
}
