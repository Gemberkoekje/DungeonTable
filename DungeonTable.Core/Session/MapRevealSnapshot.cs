namespace DungeonTable.Core.Session;

/// <summary>
/// What the table left behind on a map it is not on now: what was revealed there, which doors stood
/// open and where the projector was framed. Going back to that map brings all of it back, so a party
/// that climbs back up a floor finds it as they left it.
/// </summary>
/// <remarks>
/// The same fields as the map in play on <see cref="RevealSnapshot"/>, keyed by the map they belong to.
/// Each map has its own world coordinates, regions and doors, so none of this means anything on
/// another map; that is why it is set aside rather than carried over.
/// </remarks>
public sealed class MapRevealSnapshot
{
    /// <summary>The id of the map this was left on.</summary>
    public string MapId { get; set; } = string.Empty;

    /// <summary>Region ids revealed to the player view on that map.</summary>
    public IReadOnlyList<string> RevealedRegionIds { get; set; } = Array.Empty<string>();

    /// <summary>Feature marker and object ids revealed to the player view on that map.</summary>
    public IReadOnlyList<string> RevealedFeatureIds { get; set; } = Array.Empty<string>();

    /// <summary>The door object ids standing open on that map.</summary>
    public IReadOnlyList<string> OpenDoorIds { get; set; } = Array.Empty<string>();

    /// <summary>The grid cells painted with the fog brush on that map.</summary>
    public IReadOnlyList<FogCell> FogCells { get; set; } = Array.Empty<FogCell>();

    /// <summary>Where the player projector was framed on that map, in its world units.</summary>
    public ViewportSnapshot PlayerViewport { get; set; } = new ViewportSnapshot();
}
