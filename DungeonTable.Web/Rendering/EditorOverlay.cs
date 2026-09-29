using System.Collections.Generic;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Session;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// The editor annotation layer drawn on top of the base map in world coordinates: committed
/// regions and feature markers, object outlines, and the in-progress draft polygon, plus which
/// items are selected. Drawn only when <see cref="VectorMapRenderOptions.Overlay"/> is a non-empty
/// instance; the live DM and player map views leave it at <see cref="None"/>.
/// </summary>
public sealed class EditorOverlay
{
    /// <summary>An empty overlay (nothing is drawn).</summary>
    public static EditorOverlay None { get; } = new EditorOverlay();

    /// <summary>Committed region polygons.</summary>
    public IReadOnlyList<Region> Regions { get; init; } = Array.Empty<Region>();

    /// <summary>Committed feature markers.</summary>
    public IReadOnlyList<FeatureMarker> Markers { get; init; } = Array.Empty<FeatureMarker>();

    /// <summary>Object outlines to draw (selected and/or annotated doors and stairs).</summary>
    public IReadOnlyList<ObjectHighlight> Objects { get; init; } = Array.Empty<ObjectHighlight>();

    /// <summary>
    /// Transparent, id-tagged hit-targets drawn on top of the region fills so every door/stair
    /// stays clickable even when it sits inside a drawn region. Editor-only; empty elsewhere.
    /// </summary>
    public IReadOnlyList<ObjectHighlight> HitTargets { get; init; } = Array.Empty<ObjectHighlight>();

    /// <summary>Id of the selected region, or an empty string.</summary>
    public string SelectedRegionId { get; init; } = string.Empty;

    /// <summary>Id of the selected feature marker, or an empty string.</summary>
    public string SelectedFeatureId { get; init; } = string.Empty;

    /// <summary>Id of the selected object, or an empty string.</summary>
    public string SelectedObjectId { get; init; } = string.Empty;

    /// <summary>
    /// The last distance the DM measured, drawn as a labelled line. The live line during the drag is
    /// JS's (it has to follow the pointer), but the committed one is drawn here so it survives the
    /// re-renders a reveal causes — a JS-injected element would be wiped by the next SVG render.
    /// </summary>
    public MapMeasurement Measure { get; init; } = MapMeasurement.None;

    /// <summary>
    /// Where the DM last pointed at the projector, marked on their own map too so they can see where
    /// it landed. Static here — the pulse belongs to the player view.
    /// </summary>
    public PingMarker Ping { get; init; } = PingMarker.None;

    /// <summary>
    /// The corners of a region outline the Room Editor is drawing one click at a time, in order;
    /// empty when none is being drawn. Drawn as an open outline whose first corner is marked, since
    /// clicking it closes the outline. The segment from the last corner to the pointer is the input
    /// layer's (it has to follow the pointer), which is why the last corner is written onto the
    /// outline's group for it to read.
    /// </summary>
    public IReadOnlyList<MapPoint> Draft { get; init; } = Array.Empty<MapPoint>();

    /// <summary>
    /// True when the editor snaps the draft's next corner to the grid, so the input layer's segment
    /// to the pointer ends where the click would put that corner.
    /// </summary>
    public bool DraftSnaps { get; init; }

    /// <summary>Glyph and stroke scale in world units (typically the grid cell size).</summary>
    public double UnitSize { get; init; } = 1;

    /// <summary>True when nothing would be drawn.</summary>
    public bool IsEmpty =>
        Regions.Count == 0 && Markers.Count == 0 && Objects.Count == 0 && HitTargets.Count == 0
        && Draft.Count == 0 && !Measure.IsSet && !Ping.IsSet;
}
