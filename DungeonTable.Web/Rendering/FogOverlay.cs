using System.Collections.Generic;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// The fog-of-war layer drawn on top of the base map: an opaque (player) or translucent (DM) sheet
/// masked so it covers everything except the revealed regions and painted grid cells, plus the
/// feature markers the DM has revealed. Drawn only when <see cref="Enabled"/>; the editor and the
/// unfogged map views leave it at <see cref="None"/>.
/// </summary>
/// <remarks>
/// Player integrity: the player circuit builds this from <c>SessionState</c> using only the
/// revealed geometry, so unrevealed regions, cells and markers never reach the player DOM.
/// </remarks>
public sealed class FogOverlay
{
    /// <summary>An overlay that draws nothing.</summary>
    public static FogOverlay None { get; } = new FogOverlay();

    /// <summary>True when the fog sheet should be drawn.</summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// When true (the player view), the fog is drawn as a book-style edge: the lit area's open
    /// boundaries fade to black (hinting unexplored space beyond), while its walled boundaries get
    /// the exterior hatching treatment so a wall reads as the edge of the world — hiding whether a
    /// room lies behind it. When false (the DM view), a flat sheet is drawn instead.
    /// </summary>
    public bool BookEdge { get; init; }

    /// <summary>Fill colour of the fog sheet (an opaque dark for the player, a tint for the DM).</summary>
    public string FogColour { get; init; } = "#05060a";

    /// <summary>Opacity of the fog sheet (1 hides completely; a lower value tints for the DM).</summary>
    public double FogOpacity { get; init; } = 1;

    /// <summary>Revealed region polygons, in world units — holes punched out of the fog.</summary>
    public IReadOnlyList<IReadOnlyList<MapPoint>> RevealedPolygons { get; init; } =
        Array.Empty<IReadOnlyList<MapPoint>>();

    /// <summary>Revealed grid cells, in world units — holes punched out of the fog.</summary>
    public IReadOnlyList<MapBounds> RevealedCells { get; init; } = Array.Empty<MapBounds>();

    /// <summary>Feature markers to draw (already filtered to the revealed set for the player).</summary>
    public IReadOnlyList<FeatureMarker> RevealedMarkers { get; init; } = Array.Empty<FeatureMarker>();

    /// <summary>Glyph scale in world units (typically the grid cell size).</summary>
    public double UnitSize { get; init; } = 1;
}
