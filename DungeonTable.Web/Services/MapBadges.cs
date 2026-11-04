using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Maps;

namespace DungeonTable.Web.Services;

/// <summary>
/// The badges on the DM's map: which region carries each room's badges, and which regions are greyed
/// because their room is cleared. DM-only; the projector never draws them.
/// </summary>
/// <remarks>
/// A room drawn as several regions (split across a corridor, say) shows its badges once, under the
/// label of its largest region, and is greyed in all of them.
/// </remarks>
public sealed class MapBadges
{
    /// <summary>No badges at all: the map with them switched off.</summary>
    public static MapBadges None { get; } = new MapBadges();

    /// <summary>The badges to draw, by the id of the region whose label they sit under.</summary>
    public IReadOnlyDictionary<string, RoomBadges> ByRegion { get; init; } =
        new Dictionary<string, RoomBadges>(StringComparer.Ordinal);

    /// <summary>The regions whose room is cleared, greyed on the map.</summary>
    public IReadOnlyCollection<string> ClearedRegionIds { get; init; } = Array.Empty<string>();

    /// <summary>Works out the badges for every room on the map.</summary>
    /// <param name="regions">The map's regions.</param>
    /// <param name="glanceOf">What is in a room, by its node id; <see cref="RoomGlance.None"/> for anything that is not an authored area.</param>
    /// <param name="campaign">The table's checklist and notes.</param>
    /// <returns>The map's badges.</returns>
    public static MapBadges For(IReadOnlyList<Region> regions, Func<string, RoomGlance> glanceOf, CampaignState campaign)
    {
        ArgumentNullException.ThrowIfNull(glanceOf);
        ArgumentNullException.ThrowIfNull(campaign);

        var byRegion = new Dictionary<string, RoomBadges>(StringComparer.Ordinal);
        var cleared = new HashSet<string>(StringComparer.Ordinal);
        IEnumerable<IGrouping<string, Region>> rooms = (regions ?? Array.Empty<Region>())
            .Where(region => region is not null && region.GraphNodeId.Length > 0 && region.Polygon.Count >= 3)
            .GroupBy(region => region.GraphNodeId, StringComparer.Ordinal);

        foreach (IGrouping<string, Region> room in rooms)
        {
            RoomBadges badges = RoomBadges.For(glanceOf(room.Key), campaign);
            if (badges.IsEmpty)
            {
                continue;
            }

            byRegion[Largest(room).RegionId] = badges;
            if (badges.Cleared)
            {
                cleared.UnionWith(room.Select(region => region.RegionId));
            }
        }

        return new MapBadges { ByRegion = byRegion, ClearedRegionIds = cleared };
    }

    // The region with the largest area; the first of them on a tie.
    private static Region Largest(IEnumerable<Region> regions)
    {
        Region largest = null;
        double best = -1;
        foreach (Region region in regions)
        {
            double area = Area(region.Polygon);
            if (area > best)
            {
                best = area;
                largest = region;
            }
        }

        return largest;
    }

    private static double Area(IReadOnlyList<MapPoint> polygon)
    {
        double twice = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            MapPoint a = polygon[i];
            MapPoint b = polygon[(i + 1) % polygon.Count];
            twice += (a.X * b.Y) - (b.X * a.Y);
        }

        return Math.Abs(twice) / 2;
    }
}
