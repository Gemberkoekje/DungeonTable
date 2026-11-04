using System.Collections.Generic;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Core.Session;

namespace DungeonTable.Web.Services;

/// <summary>
/// What the player projector shows, taken as one picture under <see cref="SessionState"/>'s lock: the
/// map, what was revealed on it, its open doors and its framing. The projector stays on its map when the
/// DM moves to another one, until the DM shows the new map to the players
/// (<see cref="SessionState.ShowCurrentMapOnProjector"/>), so this is not always the DM's own map.
/// </summary>
public sealed class ProjectorView
{
    /// <summary>The map on the projector; empty before any map was chosen.</summary>
    public string MapId { get; init; } = string.Empty;

    /// <summary>True when the projector shows the map the DM is on.</summary>
    public bool IsCurrentMap { get; init; }

    /// <summary>The regions revealed on the projector's map.</summary>
    public IReadOnlyCollection<string> RevealedRegionIds { get; init; } = Array.Empty<string>();

    /// <summary>The features and objects revealed on the projector's map.</summary>
    public IReadOnlyCollection<string> RevealedFeatureIds { get; init; } = Array.Empty<string>();

    /// <summary>The doors standing open on the projector's map.</summary>
    public IReadOnlyCollection<string> OpenDoorIds { get; init; } = Array.Empty<string>();

    /// <summary>The grid cells revealed on the projector's map.</summary>
    public IReadOnlyCollection<(int Col, int Row)> FogCells { get; init; } = Array.Empty<(int, int)>();

    /// <summary>What part of the map the projector shows, in world units; empty for the whole map.</summary>
    public MapBounds Viewport { get; init; }

    /// <summary>
    /// Where the DM is pointing, or <see cref="PingMarker.None"/>. Only ever set while the projector
    /// shows the DM's map: a point on the map the DM is on would land anywhere on another.
    /// </summary>
    public PingMarker Ping { get; init; } = PingMarker.None;
}
