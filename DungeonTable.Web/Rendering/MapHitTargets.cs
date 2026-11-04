using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// The transparent click targets the DM's map lays over the objects a tool acts on. The editor
/// overlay draws them last, above the region fills: without them an object inside a drawn region
/// cannot be clicked, because the region is painted over it and takes the click. A tool that acts on
/// no objects gets none, so a click reaches the region beneath.
/// </summary>
public static class MapHitTargets
{
    /// <summary>Every door on the map: the Doors tool discovers and re-hides secret ones too.</summary>
    /// <param name="map">The map.</param>
    /// <returns>One target per door. Stairs are left out: no tool that wants doors acts on them.</returns>
    public static IReadOnlyList<ObjectHighlight> Doors(VectorMap map) =>
        map.Doors.Select(DoorTarget).ToArray();

    /// <summary>
    /// The doors a plain click in Look opens and closes: every door except a secret door the players
    /// have not found. That one gets no target, so a click on it reaches the room beneath and looks at
    /// it. Discovering a secret door takes the Doors tool, so it never happens by accident.
    /// </summary>
    /// <param name="map">The map.</param>
    /// <param name="secretDoorIds">The doors the Room Editor marked secret.</param>
    /// <param name="revealedFeatureIds">What the players have been shown, discovered secret doors among it.</param>
    /// <returns>One target per door a look may open or close.</returns>
    public static IReadOnlyList<ObjectHighlight> LookDoors(
        VectorMap map,
        IReadOnlyCollection<string> secretDoorIds,
        IReadOnlyCollection<string> revealedFeatureIds)
    {
        var secret = new HashSet<string>(secretDoorIds, StringComparer.Ordinal);
        var revealed = new HashSet<string>(revealedFeatureIds, StringComparer.Ordinal);
        return map.Doors
            .Where(door => !secret.Contains(door.Id) || revealed.Contains(door.Id))
            .Select(DoorTarget)
            .ToArray();
    }

    /// <summary>
    /// The objects the Room Editor marked concealed, for the Conceal tool. Only those: a target over
    /// every decoration on a level would be a hundred boxes taking the map's clicks.
    /// </summary>
    /// <param name="map">The map.</param>
    /// <param name="concealedObjectIds">The ids of the concealed stairs and decorations.</param>
    /// <returns>One target per concealed object.</returns>
    public static IReadOnlyList<ObjectHighlight> Concealed(VectorMap map, IReadOnlyCollection<string> concealedObjectIds)
    {
        var concealed = new HashSet<string>(concealedObjectIds, StringComparer.Ordinal);
        var list = new List<ObjectHighlight>(concealed.Count);
        foreach (MapDecoration decoration in map.Decorations.Where(d => concealed.Contains(d.Id)))
        {
            list.Add(new ObjectHighlight { Id = decoration.Id, Kind = MapObjectKind.Decoration, Bounds = decoration.Bounds });
        }

        foreach (StairFeature stair in map.Stairs.Where(s => concealed.Contains(s.Id)))
        {
            list.Add(new ObjectHighlight { Id = stair.Id, Kind = MapObjectKind.Stairs, Bounds = stair.Bounds });
        }

        return list;
    }

    private static ObjectHighlight DoorTarget(DoorFeature door) =>
        new ObjectHighlight { Id = door.Id, Kind = MapObjectKind.Door, Bounds = door.Bounds };
}
