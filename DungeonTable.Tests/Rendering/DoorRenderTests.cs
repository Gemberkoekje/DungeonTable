using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// Verifies the door presentation states (open/closed, single/double) the DM controls. A closed
/// single door keeps the source geometry look; open and double are synthesized from the opening.
/// </summary>
public sealed class DoorRenderTests
{
    private const string DoorId = "door-1";

    // One horizontal door (20 wide x 4 tall) set in an E-W wall, with source geometry so the closed
    // single case draws the leaf slab exactly as before.
    private static VectorMap MapWithOneDoor() =>
        new VectorMap
        {
            Bounds = new MapBounds(0, 0, 100, 100),
            Grid = new Grid { CellSizePx = 10 },
            Doors = new[]
            {
                new DoorFeature
                {
                    Id = DoorId,
                    Orientation = DoorOrientation.Horizontal,
                    Bounds = new MapBounds(40, 48, 60, 52),
                    Geometry = new VectorGeometry
                    {
                        Polygons = new[]
                        {
                            new MapPolygon
                            {
                                Rings = new[]
                                {
                                    new PolygonRing
                                    {
                                        Vertices = new[]
                                        {
                                            new MapPoint(40, 48), new MapPoint(60, 48),
                                            new MapPoint(60, 52), new MapPoint(40, 52), new MapPoint(40, 48),
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

    private static string Render(bool open, bool dbl)
    {
        var options = new VectorMapRenderOptions
        {
            Doors = new DoorRenderState
            {
                OpenDoorIds = open ? new[] { DoorId } : Array.Empty<string>(),
                DoubleDoorIds = dbl ? new[] { DoorId } : Array.Empty<string>(),
            },
        };
        return VectorMapSvgRenderer.Render(MapWithOneDoor(), options);
    }

    private static int Count(string haystack, string needle) =>
        (haystack.Length - haystack.Replace(needle, string.Empty).Length) / needle.Length;

    [Fact]
    public void An_open_door_renders_differently_from_a_closed_one_but_stays_a_door()
    {
        string closed = Render(open: false, dbl: false);
        string open = Render(open: true, dbl: false);

        Assert.NotEqual(closed, open);
        Assert.Contains("data-object-id=\"door-1\"", open, StringComparison.Ordinal); // still a selectable door
        Assert.Contains("fill=\"#FFFFFF\"", open, StringComparison.Ordinal);          // still a door leaf
    }

    [Fact]
    public void A_double_door_draws_two_leaves_where_a_single_draws_one()
    {
        int single = Count(Render(open: false, dbl: false), "fill=\"#FFFFFF\"");
        int dbl = Count(Render(open: false, dbl: true), "fill=\"#FFFFFF\"");

        Assert.Equal(1, single);
        Assert.Equal(2, dbl);
    }

    [Fact]
    public void An_open_double_door_draws_two_leaves()
    {
        int leaves = Count(Render(open: true, dbl: true), "fill=\"#FFFFFF\"");

        Assert.Equal(2, leaves);
    }
}
