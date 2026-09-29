using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// Verifies the fog-lifter field of view: floor tiles within the vision radius (5 ft per cell) with a
/// clear line from the viewer are revealed, sight stops at walls and at still-hidden secret doors, and a
/// tile only partly in range counts as fully revealed.
/// </summary>
public sealed class VisionRevealTests
{
    private static readonly string[] SecretDoor = { "sd" };

    // A 10x10-cell floor (cell size 10) with a solid vertical wall on the grid line at x = 50.
    private static VectorMap StripWithWallAtX50() =>
        new VectorMap
        {
            Grid = new Grid { CellSizePx = 10 },
            Bounds = new MapBounds(0, 0, 100, 100),
            Layers = new[]
            {
                new MapLayer { Kind = LayerKind.Floor, Geometry = Rect(0, 0, 100, 100) },
                new MapLayer
                {
                    Kind = LayerKind.Walls,
                    Geometry = new VectorGeometry
                    {
                        Polylines = new[] { new Polyline { Points = new[] { new MapPoint(50, 0), new MapPoint(50, 100) } } },
                    },
                },
            },
        };

    // The same floor but the wall has a gap at y 40..60, filled by a door (open, or secret = blocking).
    private static VectorMap StripWithDoorGapAtX50() =>
        new VectorMap
        {
            Grid = new Grid { CellSizePx = 10 },
            Bounds = new MapBounds(0, 0, 100, 100),
            Layers = new[]
            {
                new MapLayer { Kind = LayerKind.Floor, Geometry = Rect(0, 0, 100, 100) },
                new MapLayer
                {
                    Kind = LayerKind.Walls,
                    Geometry = new VectorGeometry
                    {
                        Polylines = new[]
                        {
                            new Polyline { Points = new[] { new MapPoint(50, 0), new MapPoint(50, 40) } },
                            new Polyline { Points = new[] { new MapPoint(50, 60), new MapPoint(50, 100) } },
                        },
                    },
                },
            },
            Doors = new[]
            {
                new DoorFeature { Id = "sd", Orientation = DoorOrientation.Vertical, Bounds = new MapBounds(48, 40, 52, 60) },
            },
        };

    private static VectorGeometry Rect(double x0, double y0, double x1, double y1) =>
        new VectorGeometry
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
                                new MapPoint(x0, y0), new MapPoint(x1, y0),
                                new MapPoint(x1, y1), new MapPoint(x0, y1), new MapPoint(x0, y0),
                            },
                        },
                    },
                },
            },
        };

    [Fact]
    public void Reveals_floor_tiles_in_range_and_stops_at_a_wall()
    {
        // 20 ft = 4 cells = radius 40 units; the viewer stands in tile (2,2) at (25,25).
        var seen = VisionReveal.Find(StripWithWallAtX50(), 25, 25, 20, System.Array.Empty<string>(), System.Array.Empty<string>());

        Assert.Contains((2, 2), seen); // the viewer's own tile
        Assert.Contains((1, 2), seen); // left, clear line
        Assert.Contains((4, 2), seen); // still left of the wall (x 40..50), clear
        Assert.DoesNotContain((5, 2), seen); // just across the wall (x 50..60) — blocked
        Assert.DoesNotContain((8, 2), seen); // far across the wall — blocked / out of range
    }

    [Fact]
    public void Tiles_beyond_the_range_are_not_revealed()
    {
        // 10 ft = 2 cells = radius 20 units.
        var seen = VisionReveal.Find(StripWithWallAtX50(), 25, 25, 10, System.Array.Empty<string>(), System.Array.Empty<string>());

        Assert.Contains((2, 2), seen);
        Assert.DoesNotContain((9, 9), seen); // far corner, well out of range
    }

    [Fact]
    public void A_tile_only_partly_in_range_is_revealed_whole()
    {
        // 11 ft = radius 22 units. Tile (0,0)'s nearest corner (10,10) is ~21.2 away (in range) while its
        // centre (5,5) is ~28.3 away (out) — the whole tile should still reveal.
        var seen = VisionReveal.Find(StripWithWallAtX50(), 25, 25, 11, System.Array.Empty<string>(), System.Array.Empty<string>());

        Assert.Contains((0, 0), seen);
    }

    [Fact]
    public void A_tile_whose_centre_a_slanted_wall_cuts_is_revealed_on_its_floor_side()
    {
        // Floor is the region below the diagonal x+y=150 (a slanted wall from (100,50) to (50,100)).
        // Tile (7,7)'s centre (75,75) sits exactly on the slant (not floor), but its lower-left corner is
        // floor and in clear view from the room — so the whole tile should reveal.
        var map = new VectorMap
        {
            Grid = new Grid { CellSizePx = 10 },
            Bounds = new MapBounds(0, 0, 100, 100),
            Layers = new[]
            {
                new MapLayer
                {
                    Kind = LayerKind.Floor,
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
                                            new MapPoint(0, 0), new MapPoint(100, 0), new MapPoint(100, 50),
                                            new MapPoint(50, 100), new MapPoint(0, 100), new MapPoint(0, 0),
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
                new MapLayer
                {
                    Kind = LayerKind.Walls,
                    Geometry = new VectorGeometry
                    {
                        Polylines = new[] { new Polyline { Points = new[] { new MapPoint(100, 50), new MapPoint(50, 100) } } },
                    },
                },
            },
        };

        var seen = VisionReveal.Find(map, 35, 75, 20, System.Array.Empty<string>(), System.Array.Empty<string>());

        Assert.Contains((7, 7), seen);
    }

    [Fact]
    public void A_secret_door_blocks_sight_but_an_open_gap_does_not()
    {
        VectorMap map = StripWithDoorGapAtX50();

        // Through the open door gap (nothing secret), sight reaches the tile beyond it.
        var open = VisionReveal.Find(map, 25, 45, 30, System.Array.Empty<string>(), System.Array.Empty<string>());
        Assert.Contains((6, 4), open);

        // Mark the door secret and it blocks sight like a wall.
        var secret = VisionReveal.Find(map, 25, 45, 30, SecretDoor, System.Array.Empty<string>());
        Assert.Contains((2, 4), secret); // the viewer's tile is still revealed
        Assert.DoesNotContain((6, 4), secret);
    }

    [Fact]
    public void Sight_does_not_leak_through_a_90_degree_wall_corner_into_the_next_room()
    {
        // Rooms A = [0,60]^2 and B = [60,120]^2 touch ONLY at the concave corner (60,60), where four walls
        // meet. A sight line from A to B threads exactly through that shared point; the strict crossing test
        // treats it as crossing neither wall, which used to leak room B into view.
        VectorMap map = TwoRoomsSharingCorner();

        var seen = VisionReveal.Find(map, 55, 55, 40, System.Array.Empty<string>(), System.Array.Empty<string>());

        Assert.Contains((5, 5), seen); // the viewer's own tile
        Assert.Contains((0, 0), seen); // elsewhere in the viewer's room, a clear line
        Assert.DoesNotContain((6, 6), seen); // across the corner into room B — must NOT leak
    }

    private static VectorMap TwoRoomsSharingCorner() =>
        new VectorMap
        {
            Grid = new Grid { CellSizePx = 10 },
            Bounds = new MapBounds(0, 0, 120, 120),
            Layers = new[]
            {
                new MapLayer
                {
                    Kind = LayerKind.Floor,
                    Geometry = new VectorGeometry { Polygons = new[] { Square(0, 0, 60, 60), Square(60, 60, 120, 120) } },
                },
                new MapLayer
                {
                    Kind = LayerKind.Walls,
                    Geometry = new VectorGeometry
                    {
                        Polylines = new[]
                        {
                            new Polyline { Points = new[] { new MapPoint(60, 0), new MapPoint(60, 60) } },
                            new Polyline { Points = new[] { new MapPoint(0, 60), new MapPoint(60, 60) } },
                            new Polyline { Points = new[] { new MapPoint(60, 60), new MapPoint(120, 60) } },
                            new Polyline { Points = new[] { new MapPoint(60, 60), new MapPoint(60, 120) } },
                        },
                    },
                },
            },
        };

    private static MapPolygon Square(double x0, double y0, double x1, double y1) =>
        new MapPolygon
        {
            Rings = new[]
            {
                new PolygonRing
                {
                    Vertices = new[]
                    {
                        new MapPoint(x0, y0), new MapPoint(x1, y0),
                        new MapPoint(x1, y1), new MapPoint(x0, y1), new MapPoint(x0, y0),
                    },
                },
            },
        };

    [Fact]
    public void An_open_door_lets_sight_pass_through_a_solid_wall()
    {
        // A door object sits on the solid wall at x = 50 (a door drawn over an unbroken wall).
        VectorMap map = StripWithWalledDoorAtX50();

        // Closed, the wall blocks sight — the tile across it is not seen.
        var closed = VisionReveal.Find(map, 25, 45, 30, System.Array.Empty<string>(), System.Array.Empty<string>());
        Assert.DoesNotContain((6, 4), closed);

        // Opened, sight passes through the doorway and reveals the tile beyond.
        var open = VisionReveal.Find(map, 25, 45, 30, System.Array.Empty<string>(), OpenWalledDoor);
        Assert.Contains((6, 4), open);
    }

    private static readonly string[] OpenWalledDoor = { "wd" };

    private static VectorMap StripWithWalledDoorAtX50()
    {
        VectorMap baseMap = StripWithWallAtX50();
        return new VectorMap
        {
            Grid = baseMap.Grid,
            Bounds = baseMap.Bounds,
            Layers = baseMap.Layers,
            Doors = new[]
            {
                new DoorFeature { Id = "wd", Orientation = DoorOrientation.Vertical, Bounds = new MapBounds(48, 40, 52, 60) },
            },
        };
    }
}
