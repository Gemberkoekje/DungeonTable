namespace DungeonTable.Core.Session;

/// <summary>
/// The stored party token: whether it is on the map, where, and how far the party sees. A plain
/// document shape beside <see cref="PartyToken"/>, as <see cref="ViewportSnapshot"/> is beside the
/// map's bounds, so the stored fields are exactly these four.
/// </summary>
/// <remarks>
/// Additive and default-empty: a document written before the token existed restores with no token
/// and the default vision range, and a build that predates the token ignores it, so
/// <see cref="TableSnapshot.CurrentVersion"/> was not bumped for it.
/// </remarks>
public sealed class PartyTokenSnapshot
{
    /// <summary>True when the token was on the map.</summary>
    public bool Placed { get; set; }

    /// <summary>Where the token stood, in map world units.</summary>
    public double X { get; set; }

    /// <summary>Where the token stood, in map world units.</summary>
    public double Y { get; set; }

    /// <summary>The vision range in feet; 0 (a document from before the token) reads as the default.</summary>
    public int VisionFeet { get; set; }
}
