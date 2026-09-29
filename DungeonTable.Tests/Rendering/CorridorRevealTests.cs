using System.Collections.Generic;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// Verifies the corridor flood fill: it reaches floor through open doorways and is stopped by walls,
/// so a room behind a wall is never revealed while an open passage is.
/// </summary>
public sealed class CorridorRevealTests
{
    // A 5-cell horizontal floor strip (cells 0..4 on row 0), cell size 10, with a single wall on the
    // grid line at x = 30 — between cell 2 and cell 3.
    private static VectorMap StripMapWithWallAtX30() =>
        new VectorMap
        {
            Grid = new Grid { CellSizePx = 10 },
            Bounds = new MapBounds(0, 0, 50, 10),
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
                                            new MapPoint(0, 0), new MapPoint(50, 0),
                                            new MapPoint(50, 10), new MapPoint(0, 10), new MapPoint(0, 0),
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
                        Polylines = new[] { new Polyline { Points = new[] { new MapPoint(30, 0), new MapPoint(30, 10) } } },
                    },
                },
            },
        };

    // A region covering cell 2 only (world 20..30 x 0..10).
    private static readonly IReadOnlyList<MapPoint> Cell2Room = new[]
    {
        new MapPoint(20, 0), new MapPoint(30, 0), new MapPoint(30, 10), new MapPoint(20, 10),
    };

    // A 5-cell floor strip with an OPEN doorway (a gap in the wall) at x = 30, plus a door object in
    // that gap — so the same map reads as passable, or blocked, depending only on whether the door is
    // treated as secret.
    private const string SecretDoorId = "sd-strip";

    private static VectorMap StripMapWithDoorAtX30() =>
        new VectorMap
        {
            Grid = new Grid { CellSizePx = 10 },
            Bounds = new MapBounds(0, 0, 50, 10),
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
                                            new MapPoint(0, 0), new MapPoint(50, 0),
                                            new MapPoint(50, 10), new MapPoint(0, 10), new MapPoint(0, 0),
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            },
            Doors = new[]
            {
                new DoorFeature
                {
                    Id = SecretDoorId,
                    Orientation = DoorOrientation.Vertical,
                    Bounds = new MapBounds(28, 0, 32, 10),
                },
            },
        };

    [Fact]
    public void The_fill_reaches_through_the_opening_but_not_across_the_wall()
    {
        var reached = CorridorReveal.Find(
            StripMapWithWallAtX30(), new[] { Cell2Room }, Array.Empty<MapBounds>(), steps: 1, Array.Empty<string>(), Array.Empty<string>())
            .Select(r => r.Cell).ToList();

        // Cell 1 (10..20) is open to the room → reached. Cell 3 (30..40) is behind the wall → not.
        Assert.Contains(new MapBounds(10, 0, 20, 10), reached);
        Assert.DoesNotContain(new MapBounds(30, 0, 40, 10), reached);
    }

    [Fact]
    public void The_fill_stops_at_the_step_limit_and_tags_distance()
    {
        var reached = CorridorReveal.Find(
            StripMapWithWallAtX30(), new[] { Cell2Room }, Array.Empty<MapBounds>(), steps: 1, Array.Empty<string>(), Array.Empty<string>());

        // One step left of the room reaches cell 1 (distance 1) but not cell 0.
        Assert.Contains((new MapBounds(10, 0, 20, 10), 1), reached);
        Assert.DoesNotContain(new MapBounds(0, 0, 10, 10), reached.Select(r => r.Cell));
    }

    [Fact]
    public void An_open_door_lets_the_fill_through_but_a_secret_door_blocks_it()
    {
        // The door at x = 30 is a plain gap: with nothing secret, the peek runs through it to cell 3.
        var throughOpen = CorridorReveal.Find(
            StripMapWithDoorAtX30(), new[] { Cell2Room }, Array.Empty<MapBounds>(), steps: 1, Array.Empty<string>(), Array.Empty<string>())
            .Select(r => r.Cell).ToList();
        Assert.Contains(new MapBounds(30, 0, 40, 10), throughOpen);

        // Mark that door secret and it stops the fill like a wall — cell 3 (behind it) is never reached.
        var throughSecret = CorridorReveal.Find(
            StripMapWithDoorAtX30(), new[] { Cell2Room }, Array.Empty<MapBounds>(), steps: 1, new[] { SecretDoorId }, Array.Empty<string>())
            .Select(r => r.Cell).ToList();
        Assert.DoesNotContain(new MapBounds(30, 0, 40, 10), throughSecret);
        Assert.Contains(new MapBounds(10, 0, 20, 10), throughSecret); // the open side is still reached
    }

    [Fact]
    public void A_map_without_floor_yields_no_corridors()
    {
        var bare = new VectorMap { Grid = new Grid { CellSizePx = 10 }, Bounds = new MapBounds(0, 0, 50, 10) };

        var reached = CorridorReveal.Find(bare, new[] { Cell2Room }, Array.Empty<MapBounds>(), steps: 2, Array.Empty<string>(), Array.Empty<string>());

        Assert.Empty(reached);
    }

    [Fact]
    public void An_open_door_lets_the_fill_run_through_a_solid_wall()
    {
        // A door object sits on the solid wall at x = 30 (like a door drawn over an unbroken wall).
        VectorMap map = StripMapWithWalledDoorAtX30();

        // Closed, the wall blocks the fill — cell 3 (behind it) is not reached.
        var closed = CorridorReveal.Find(
            map, new[] { Cell2Room }, Array.Empty<MapBounds>(), steps: 1, Array.Empty<string>(), Array.Empty<string>())
            .Select(r => r.Cell).ToList();
        Assert.DoesNotContain(new MapBounds(30, 0, 40, 10), closed);

        // Opened, the fill runs through the doorway and reveals the tile behind it.
        var open = CorridorReveal.Find(
            map, new[] { Cell2Room }, Array.Empty<MapBounds>(), steps: 1, Array.Empty<string>(), WalledDoor)
            .Select(r => r.Cell).ToList();
        Assert.Contains(new MapBounds(30, 0, 40, 10), open);
    }

    private const string WalledDoorId = "wd";
    private static readonly string[] WalledDoor = { WalledDoorId };

    private static VectorMap StripMapWithWalledDoorAtX30()
    {
        VectorMap baseMap = StripMapWithWallAtX30();
        return new VectorMap
        {
            Grid = baseMap.Grid,
            Bounds = baseMap.Bounds,
            Layers = baseMap.Layers,
            Doors = new[]
            {
                new DoorFeature
                {
                    Id = WalledDoorId,
                    Orientation = DoorOrientation.Vertical,
                    Bounds = new MapBounds(28, 0, 32, 10),
                },
            },
        };
    }
}
