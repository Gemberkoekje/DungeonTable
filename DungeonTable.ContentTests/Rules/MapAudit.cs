using System.Collections.Generic;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Infrastructure.Maps;
using DungeonTable.Web.Services;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.ContentTests.Rules;

/// <summary>
/// The rules for a map's annotations: the file is where a save would write it, the map has a drawing,
/// every region can be clicked and opens an area, every area on a mapped floor can be clicked, and no
/// region covers another's middle.
/// </summary>
internal static class MapAudit
{
    // Rows of a region's bounding box to look for its inside along, the middle first.
    private static readonly double[] Rows = { 0.5, 0.25, 0.75, 0.125, 0.375, 0.625, 0.875 };

    /// <summary>
    /// A document whose map id or adventure is not its file's. The store finds a map by its file name,
    /// and the Room Editor saves it under its map id in its adventure's folder, so a save would write a
    /// second file and leave this one as it was.
    /// </summary>
    /// <param name="relativePath">The document's path under the maps folder ("demo/level-1-undercroft.regions.json").</param>
    /// <param name="map">The document, as written.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> Misfiled(string relativePath, MapDefinition map)
    {
        int slash = relativePath.LastIndexOf('/');
        string folder = slash < 0 ? string.Empty : relativePath[..slash];
        string file = relativePath[(slash + 1)..];
        string id = file[..^FileSystemMapStore.DefinitionSuffix.Length];

        var faults = new List<string>();
        if (!string.Equals(map.MapId, id, StringComparison.Ordinal))
        {
            faults.Add($"Its mapId is '{map.MapId}', but the file is {file}: the map is found by its file name and saved under its mapId.");
        }

        if (!string.Equals(map.Adventure ?? string.Empty, folder, StringComparison.Ordinal))
        {
            faults.Add($"Its adventure is '{map.Adventure}', but the file is in '{folder}': a save would write it to the '{map.Adventure}' folder instead.");
        }

        return faults;
    }

    /// <summary>A map id another annotation document has too, or no drawing has. Either way the map cannot be opened as written.</summary>
    /// <param name="fileName">This document's name.</param>
    /// <param name="map">This document, as written.</param>
    /// <param name="others">Every annotation document, by name.</param>
    /// <param name="drawings">The ids of the maps' drawings (their <c>.ds</c> file names).</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> Unopenable(string fileName, MapDefinition map, IEnumerable<(string File, MapDefinition Map)> others, IReadOnlySet<string> drawings)
    {
        var faults = others
            .Where(other => !string.Equals(other.File, fileName, StringComparison.Ordinal)
                && string.Equals(other.Map.MapId, map.MapId, StringComparison.Ordinal))
            .Select(other => $"{other.File} annotates a map with the same id, '{map.MapId}': only one of them can be opened.")
            .ToList();

        if (!drawings.Contains(map.MapId ?? string.Empty))
        {
            faults.Add($"No {map.MapId}{FileSystemVectorMapStore.SourceSuffix} draws the map these regions annotate, and the DM screen lists a map by its drawing.");
        }

        return faults;
    }

    /// <summary>
    /// Every region with no id or a repeated one (the reveal state records regions by id), or fewer than
    /// three corners, which cannot be clicked or revealed.
    /// </summary>
    /// <param name="map">The document, as written.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> BadRegions(MapDefinition map)
    {
        var faults = new List<string>();
        for (int i = 0; i < map.Regions.Count; i++)
        {
            Region region = map.Regions[i];
            string which = $"The region {Check.Named(region.RegionId, $"regions[{i}]")} ('{region.Label}')";
            Check.Written(faults, region.RegionId, $"{which} has no id, and a revealed region is recorded by it.");
            if (region.Polygon.Count < 3)
            {
                faults.Add($"{which} has {region.Polygon.Count} corners, and fewer than three cannot be clicked or revealed.");
            }
        }

        faults.AddRange(Check.Repeated(map.Regions.Select(region => region.RegionId))
            .Select(id => $"Two regions have the id '{id}': revealing one would reveal both."));
        return faults;
    }

    /// <summary>
    /// Every region linked to something that is not an authored area. A link may wait for its floor, as
    /// a quest beat's destination does, by naming the id an area on a floor with no level file yet will
    /// have (<see cref="AreaIds"/>); a link to a written floor has to name an area that floor has.
    /// </summary>
    /// <param name="map">The document, as written.</param>
    /// <param name="dossiers">The loaded content.</param>
    /// <param name="floorsWithAFile">The floor numbers that have a level file.</param>
    /// <returns>One sentence per region (empty when there are none).</returns>
    internal static IReadOnlyList<string> BadLinks(MapDefinition map, IDossierStore dossiers, IReadOnlySet<int> floorsWithAFile) =>
        map.Regions
            .Where(region => !string.IsNullOrWhiteSpace(region.GraphNodeId) && !dossiers.GetArea(region.GraphNodeId).IsValid)
            .Where(region => !AreaIds.TryFloor(region.GraphNodeId, out int floor) || floorsWithAFile.Contains(floor))
            .Select(region => $"The region '{region.RegionId}' ('{region.Label}') opens '{region.GraphNodeId}', which is no area a level file writes.")
            .ToList();

    /// <summary>
    /// Every area on a floor this map shows that no map lets the DM click: it has no region, and no
    /// area with the same number does. An area that sub-areas fill completely ("Area 3 is 3a and 3b")
    /// has no floor of its own to click, and is reached from its sub-areas instead.
    /// </summary>
    /// <param name="map">The document, as written.</param>
    /// <param name="allMaps">Every map's annotations, this one included.</param>
    /// <param name="dossiers">The loaded content.</param>
    /// <returns>One sentence per area (empty when there are none).</returns>
    internal static IReadOnlyList<string> Unclickable(MapDefinition map, IEnumerable<MapDefinition> allMaps, IDossierStore dossiers)
    {
        var clickable = allMaps
            .SelectMany(other => other.Regions)
            .Select(region => region.GraphNodeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        var floors = map.Regions
            .Where(region => dossiers.GetArea(region.GraphNodeId ?? string.Empty).IsValid)
            .Select(region => FloorOf(region.GraphNodeId, dossiers))
            .Where(floor => floor.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        AreaDossier[] shown = dossiers.AllAreas().Where(area => floors.Contains(FloorOf(area.AreaNodeId, dossiers))).ToArray();
        return shown
            .Where(area => !clickable.Contains(area.AreaNodeId))
            .Where(area => !shown.Any(other => other.AreaNumber == area.AreaNumber
                && !string.Equals(other.AreaNodeId, area.AreaNodeId, StringComparison.Ordinal)
                && clickable.Contains(other.AreaNodeId)))
            .Select(area => $"{area.Title} ('{area.AreaNodeId}') has no region on any map, and no area with its number does.")
            .ToList();
    }

    /// <summary>
    /// Every region whose middle another region covers. A click there reaches only one of the two, and
    /// revealing one shows part of the other: the trap an umbrella area's outline drawn over its
    /// sub-areas falls into. Tested on the outlines themselves, as a click is.
    /// </summary>
    /// <param name="map">The document, as written.</param>
    /// <returns>One sentence per pair (empty when there are none).</returns>
    internal static IReadOnlyList<string> Covered(MapDefinition map)
    {
        Region[] regions = map.Regions.Where(region => region.Polygon.Count >= 3).ToArray();
        var faults = new List<string>();
        foreach (Region inner in regions)
        {
            MapPoint middle = Inside(inner);
            faults.AddRange(regions
                .Where(outer => !ReferenceEquals(outer, inner) && Contains(outer, middle))
                .Select(outer => $"'{Name(outer)}' covers the middle of '{Name(inner)}'."));
        }

        return faults;
    }

    /// <summary>
    /// A point inside a region: the middle of its bounding box when that is inside it, and otherwise the
    /// middle of the widest stretch of it along a row of the box. An L or a U can leave its box's middle
    /// outside itself.
    /// </summary>
    /// <param name="region">A region with at least three corners.</param>
    /// <returns>The point.</returns>
    internal static MapPoint Inside(Region region)
    {
        MapBounds box = MapFraming.Bounds(new[] { region });
        foreach (double row in Rows)
        {
            double y = box.MinY + (box.Height * row);
            var middle = new MapPoint(box.MinX + (box.Width / 2), y);
            if (Contains(region, middle))
            {
                return middle;
            }

            (double Left, double Right)[] stretches = Stretches(region.Polygon, y).ToArray();
            if (stretches.Length > 0)
            {
                (double left, double right) = stretches.MaxBy(stretch => stretch.Right - stretch.Left);
                if (right > left)
                {
                    return new MapPoint((left + right) / 2, y);
                }
            }
        }

        return new MapPoint(box.MinX + (box.Width / 2), box.MinY + (box.Height / 2));
    }

    // The app's own hit test for a region.
    private static bool Contains(Region region, MapPoint point) =>
        MapFraming.RegionAt(new[] { region }, point.X, point.Y).Length > 0;

    // Where a horizontal line crosses the outline, in pairs: the stretches of the line inside it.
    private static IEnumerable<(double Left, double Right)> Stretches(IReadOnlyList<MapPoint> polygon, double y)
    {
        var crossings = new List<double>();
        for (int i = 0; i < polygon.Count; i++)
        {
            MapPoint a = polygon[i];
            MapPoint b = polygon[(i + 1) % polygon.Count];
            if ((a.Y <= y && b.Y > y) || (b.Y <= y && a.Y > y))
            {
                crossings.Add(a.X + ((y - a.Y) * (b.X - a.X) / (b.Y - a.Y)));
            }
        }

        crossings.Sort();
        for (int i = 0; i + 1 < crossings.Count; i += 2)
        {
            yield return (crossings[i], crossings[i + 1]);
        }
    }

    private static string FloorOf(string areaNodeId, IDossierStore dossiers)
    {
        Result<LevelDossier> level = dossiers.GetLevelForArea(areaNodeId);
        return level.IsValid ? level.Value.LevelNodeId ?? string.Empty : string.Empty;
    }

    private static string Name(Region region) => string.IsNullOrWhiteSpace(region.Label) ? region.RegionId : region.Label;
}
