using DungeonTable.Core.Maps;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// One measurement taken on the map: the two points the DM dragged between, and how far apart they
/// are — in the grid squares 5e actually counts, in the feet that comes to, and in the true
/// straight-line distance.
/// </summary>
/// <remarks>
/// Both numbers are kept because they answer different questions. <see cref="Feet"/> is the rule as
/// written ("count each square as 5 feet, diagonals included"), which is what settles whether a move
/// or a reach is legal. <see cref="StraightFeet"/> is the geometry, which is what a DM wants when
/// deciding whether something is roughly within a 60-foot cone. On a pure diagonal the rule
/// under-counts by a factor of √2 — 20 ft counted is 28 ft of dungeon — so reporting either alone
/// would mislead in one direction or the other.
/// </remarks>
public sealed class MapMeasurement
{
    /// <summary>Nothing measured yet.</summary>
    public static MapMeasurement None { get; } = new MapMeasurement();

    /// <summary>True when this holds a real measurement (rather than "nothing measured yet").</summary>
    public bool IsSet { get; init; }

    /// <summary>
    /// True when the map carries a calibrated grid, so <see cref="Squares"/> and <see cref="Feet"/>
    /// mean something. False leaves both zero rather than inventing a scale.
    /// </summary>
    public bool HasGrid { get; init; }

    /// <summary>Where the drag started, in world units.</summary>
    public MapPoint From { get; init; }

    /// <summary>Where the drag ended, in world units.</summary>
    public MapPoint To { get; init; }

    /// <summary>Grid squares between the two points, counting a diagonal as one square (5e's rule).</summary>
    public int Squares { get; init; }

    /// <summary>The 5e distance: <see cref="Squares"/> at 5 ft each.</summary>
    public int Feet { get; init; }

    /// <summary>The true straight-line distance in feet, rounded to the nearest foot.</summary>
    public int StraightFeet { get; init; }

    /// <summary>The short form drawn on the map itself ("35 ft"), or an empty string when unset.</summary>
    public string ShortLabel
    {
        get
        {
            if (!IsSet)
            {
                return string.Empty;
            }

            return HasGrid ? $"{Feet} ft" : $"~{StraightFeet} ft";
        }
    }
}
