using System.Collections.Generic;
using DungeonTable.Core.Maps.Vector;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// Toggles for <see cref="VectorMapSvgRenderer"/>. Defaults produce the full book-style map;
/// the player circuit can turn layers off (for example the grid) without changing the source.
/// </summary>
public sealed class VectorMapRenderOptions
{
    /// <summary>The full book-style map with every layer on.</summary>
    public static VectorMapRenderOptions Default { get; } = new VectorMapRenderOptions();

    /// <summary>Draw the calibrated grid (clipped to the floor).</summary>
    public bool ShowGrid { get; init; } = true;

    /// <summary>Draw the offset inner-wall drop shadow onto the floor. Off by default — it reads
    /// as an odd double line; the outer wall shading carries the depth instead.</summary>
    public bool ShowShadow { get; init; }

    /// <summary>Draw the book-style outer-wall hatching band and soft exterior shading.</summary>
    public bool ShowWallShading { get; init; } = true;

    /// <summary>Draw the door and stair object pass.</summary>
    public bool ShowObjects { get; init; } = true;

    /// <summary>Padding, in map units, added around the geometry bounds in the viewBox.</summary>
    public double Padding { get; init; } = 24;

    /// <summary>
    /// Overrides the SVG viewBox to this world-unit rectangle (used by the player view to show
    /// only the DM-chosen region). When its width/height are non-positive the full padded bounds
    /// are used instead.
    /// </summary>
    public MapBounds ViewBox { get; init; }

    /// <summary>
    /// The editor annotation overlay to draw on top of the map (regions, markers, object outlines,
    /// and the in-progress draft polygon). Empty by default, so only the Room Editor draws it.
    /// </summary>
    public EditorOverlay Overlay { get; init; } = EditorOverlay.None;

    /// <summary>
    /// Per-door presentation state (secret / discovered / open / double), keyed by door object id.
    /// A still-secret door is drawn as a plain wall segment so it is indistinguishable from the wall
    /// (see <see cref="MarkSecretDoors"/>); the rest control open/closed and single/double leaves.
    /// </summary>
    public DoorRenderState Doors { get; init; } = DoorRenderState.None;

    /// <summary>
    /// When true (the DM view), secret doors additionally carry an "S" mark and stay selectable;
    /// when false (the player view), they render as an anonymous wall with no marker or object hook.
    /// </summary>
    public bool MarkSecretDoors { get; init; }

    /// <summary>
    /// Which stairs and decorations are concealed from the players and which the DM has revealed.
    /// A still-hidden object is drawn ghosted here (this render only ever feeds a DM-side view — the
    /// projector uses <see cref="VectorMapSvgRenderer.RenderRevealed"/>, which omits it outright).
    /// Empty by default, so an unannotated map is unaffected.
    /// </summary>
    public ConcealState Conceal { get; init; } = ConcealState.None;

    /// <summary>
    /// The fog-of-war layer to draw over the map (revealed regions/cells punch holes; the rest is
    /// covered). Empty by default, so only the DM and player map views draw it.
    /// </summary>
    public FogOverlay Fog { get; init; } = FogOverlay.None;

    /// <summary>
    /// World-unit rectangles punched OUT of the floor before it is filled, clipped, shaded and
    /// hatched — so whatever sits over one renders as wall (hatch + halo, no parchment, no grid), not
    /// floor. The player reveal passes its secret-door footprints here so a still-secret door is drawn
    /// exactly like the wall around it. Empty by default, so the DM view is unaffected.
    /// </summary>
    public IReadOnlyList<MapBounds> FloorHoles { get; init; } = Array.Empty<MapBounds>();
}
