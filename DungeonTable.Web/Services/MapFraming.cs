using System;
using System.Collections.Generic;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Web.Services;

/// <summary>
/// World-unit framing helpers shared by the DM shell: turning region polygons into rectangles, and
/// turning a rectangle into a player-projector viewport of the right aspect. All pure geometry —
/// no state, no rendering — so the "frame this room for the players" and "show this room on the
/// orientation mini map" controls agree on what a room's rectangle is.
/// </summary>
public static class MapFraming
{
    // Fall back to widescreen when a caller has no sane aspect yet (the player circuit reports the
    // real one as soon as the projector connects).
    private const double DefaultAspect = 16.0 / 9.0;

    /// <summary>The bounding rectangle of a set of region polygons; empty when none has geometry.</summary>
    /// <param name="regions">The regions to bound.</param>
    /// <returns>The union of the regions' vertex extents, in world units.</returns>
    public static MapBounds Bounds(IEnumerable<Region> regions)
    {
        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;
        bool any = false;

        foreach (Region region in regions)
        {
            foreach (MapPoint point in region.Polygon)
            {
                any = true;
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
        }

        return any ? new MapBounds(minX, minY, maxX, maxY) : default;
    }

    /// <summary>Grows a rectangle by the same margin on every side.</summary>
    /// <param name="bounds">The rectangle to pad.</param>
    /// <param name="margin">The margin in world units; non-positive values leave it unchanged.</param>
    /// <returns>The padded rectangle.</returns>
    public static MapBounds Pad(MapBounds bounds, double margin)
    {
        if (margin <= 0)
        {
            return bounds;
        }

        return new MapBounds(
            bounds.MinX - margin,
            bounds.MinY - margin,
            bounds.MaxX + margin,
            bounds.MaxY + margin);
    }

    /// <summary>
    /// Enlarges a rectangle to at least a minimum size, keeping its centre. Stops the mini map from
    /// zooming absurdly far into a one-cell closet.
    /// </summary>
    /// <param name="bounds">The rectangle to grow.</param>
    /// <param name="minimum">The minimum width and height in world units.</param>
    /// <returns>The grown rectangle.</returns>
    public static MapBounds AtLeast(MapBounds bounds, double minimum)
    {
        if (minimum <= 0)
        {
            return bounds;
        }

        double width = Math.Max(bounds.Width, minimum);
        double height = Math.Max(bounds.Height, minimum);
        double cx = bounds.MinX + (bounds.Width / 2);
        double cy = bounds.MinY + (bounds.Height / 2);
        return new MapBounds(cx - (width / 2), cy - (height / 2), cx + (width / 2), cy + (height / 2));
    }

    /// <summary>
    /// The smallest viewport of the projector's aspect ratio that contains <paramref name="target"/>,
    /// centred on it, shrunk to fit the map and clamped inside it. An empty target falls back to
    /// <see cref="DefaultViewport"/>.
    /// </summary>
    /// <param name="target">The rectangle to frame, in world units.</param>
    /// <param name="aspect">The player screen's width / height ratio.</param>
    /// <param name="map">The map's own bounds, which the viewport may not leave.</param>
    /// <returns>The framed viewport.</returns>
    public static MapBounds Frame(MapBounds target, double aspect, MapBounds map)
    {
        double ratio = double.IsFinite(aspect) && aspect > 0 ? aspect : DefaultAspect;
        if (target.Width <= 0 || target.Height <= 0)
        {
            return DefaultViewport(map, ratio);
        }

        double width = Math.Max(target.Width, target.Height * ratio);
        double height = width / ratio;

        if (map.Width > 0 && width > map.Width)
        {
            width = map.Width;
            height = width / ratio;
        }

        if (map.Height > 0 && height > map.Height)
        {
            height = map.Height;
            width = height * ratio;
        }

        double cx = target.MinX + (target.Width / 2);
        double cy = target.MinY + (target.Height / 2);
        var framed = new MapBounds(cx - (width / 2), cy - (height / 2), cx + (width / 2), cy + (height / 2));
        return ClampToMap(framed, map);
    }

    /// <summary>
    /// Keeps a viewport inside the map: shrunk to fit when it is larger, then slid so both edges
    /// land within the map's bounds.
    /// </summary>
    /// <param name="viewport">The viewport to constrain.</param>
    /// <param name="bounds">The map's bounds; an empty map leaves the viewport untouched.</param>
    /// <returns>The constrained viewport.</returns>
    public static MapBounds ClampToMap(MapBounds viewport, MapBounds bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return viewport;
        }

        double width = Math.Min(viewport.Width, bounds.Width);
        double height = Math.Min(viewport.Height, bounds.Height);
        double x = Math.Clamp(viewport.MinX, bounds.MinX, bounds.MaxX - width);
        double y = Math.Clamp(viewport.MinY, bounds.MinY, bounds.MaxY - height);
        return new MapBounds(x, y, x + width, y + height);
    }

    /// <summary>
    /// The starting player viewport for a freshly loaded map: the widest rectangle of the
    /// projector's aspect ratio that fits, centred on the map.
    /// </summary>
    /// <param name="bounds">The map's bounds.</param>
    /// <param name="aspect">The player screen's width / height ratio.</param>
    /// <returns>The seeded viewport.</returns>
    public static MapBounds DefaultViewport(MapBounds bounds, double aspect)
    {
        double ratio = double.IsFinite(aspect) && aspect > 0 ? aspect : DefaultAspect;
        double width = bounds.Width;
        double height = width / ratio;
        if (height > bounds.Height)
        {
            height = bounds.Height;
            width = height * ratio;
        }

        double x = bounds.MinX + ((bounds.Width - width) / 2);
        double y = bounds.MinY + ((bounds.Height - height) / 2);
        return new MapBounds(x, y, x + width, y + height);
    }

    /// <summary>
    /// The id of the first region whose polygon contains a world point — used to name what the
    /// player viewport is currently pointed at. An empty string when the point is in no region.
    /// </summary>
    /// <param name="regions">The map's regions.</param>
    /// <param name="worldX">World X.</param>
    /// <param name="worldY">World Y.</param>
    /// <returns>The containing region's id, or an empty string.</returns>
    public static string RegionAt(IReadOnlyList<Region> regions, double worldX, double worldY)
    {
        foreach (Region region in regions)
        {
            if (region.Polygon.Count >= 3
                && CorridorReveal.PointInRings(worldX, worldY, new[] { region.Polygon }))
            {
                return region.RegionId;
            }
        }

        return string.Empty;
    }
}
