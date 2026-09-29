namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// One z-ordered layer of the map's template stack. Drawable layers (floor, walls, mask)
/// carry resolved <see cref="Geometry"/> plus <see cref="Stroke"/>/<see cref="Fill"/>;
/// a <see cref="LayerKind.Folder"/> carries <see cref="Children"/> clipped to its
/// <see cref="ClipMask"/>; the parametric book-style effect layers (buffer shading,
/// hatching, shadow) are captured structurally by <see cref="Kind"/> and z-order, with
/// their detailed render parameters deferred to the book-style rendering phase.
/// <see cref="IsFog"/> and <see cref="AllowsLightToPass"/> feed the DM/player visibility
/// filter.
/// </summary>
public sealed class MapLayer
{
    /// <summary>Source layer name; may be empty. Identify by <see cref="Kind"/>, not name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The role this layer plays in the stack.</summary>
    public LayerKind Kind { get; init; } = LayerKind.None;

    /// <summary>Index within the parent's children (0 = bottom, painted first).</summary>
    public int ZOrder { get; init; }

    /// <summary>Per-node render toggle.</summary>
    public bool Visible { get; init; } = true;

    /// <summary>Node opacity in the 0..1 range.</summary>
    public double Alpha { get; init; } = 1;

    /// <summary>Fog flag from the source multipolygon; used for player-view filtering.</summary>
    public bool IsFog { get; init; }

    /// <summary>Whether the owning geometry lets light pass; used for occlusion/visibility.</summary>
    public bool AllowsLightToPass { get; init; }

    /// <summary>True for a mask layer: used as a clip region, not drawn.</summary>
    public bool IsMask { get; init; }

    /// <summary>Resolved geometry for drawable layers; empty for effect and folder layers.</summary>
    public VectorGeometry Geometry { get; init; } = VectorGeometry.Empty;

    /// <summary>Outline style.</summary>
    public StrokeStyle Stroke { get; init; } = new StrokeStyle();

    /// <summary>Fill style.</summary>
    public FillStyle Fill { get; init; } = new FillStyle();

    /// <summary>Child layers of a folder, in z-order; empty otherwise.</summary>
    public IReadOnlyList<MapLayer> Children { get; init; } = Array.Empty<MapLayer>();

    /// <summary>Geometry of a folder's mask child, clipping its other children; empty otherwise.</summary>
    public VectorGeometry ClipMask { get; init; } = VectorGeometry.Empty;
}
