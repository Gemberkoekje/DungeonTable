namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// The role a <see cref="MapLayer"/> plays in the template stack, identified from the
/// source node type (and, for multipolygons, the mask flag / name). Keying render and
/// visibility logic on this is more robust than matching layer names.
/// </summary>
public enum LayerKind
{
    /// <summary>Unclassified layer.</summary>
    None = 0,

    /// <summary>Soft shading band just inside the outer walls.</summary>
    BufferShading,

    /// <summary>Outer-wall cross-hatch decoration.</summary>
    Hatching,

    /// <summary>Filled floor area.</summary>
    Floor,

    /// <summary>Clip region (mask), used to bound other layers rather than drawn.</summary>
    Mask,

    /// <summary>Inner-wall drop shadow onto the floor.</summary>
    Shadow,

    /// <summary>Square grid overlay.</summary>
    Grid,

    /// <summary>Wall outlines (the black strokes).</summary>
    Walls,

    /// <summary>A grouping layer whose children are clipped to its mask.</summary>
    Folder,
}
