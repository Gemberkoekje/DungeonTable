namespace DungeonTable.Core.Session;

/// <summary>
/// The stored player viewport rectangle, in map world units. Deliberately its own type rather than
/// the rendering pipeline's <c>MapBounds</c>: that is a record struct whose <c>Width</c> /
/// <c>Height</c> / <c>IsEmpty</c> are derived, and persisting it would either write derived fields
/// into the document or need serialization attributes on a shared geometry type. Four numbers here
/// keep the stored shape purpose-built and leave the map types untouched.
/// </summary>
public sealed class ViewportSnapshot
{
    /// <summary>Left edge.</summary>
    public double MinX { get; set; }

    /// <summary>Top edge (Y grows downward).</summary>
    public double MinY { get; set; }

    /// <summary>Right edge.</summary>
    public double MaxX { get; set; }

    /// <summary>Bottom edge.</summary>
    public double MaxY { get; set; }
}
