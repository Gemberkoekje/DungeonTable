using System.Collections.Generic;
using System.Threading;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Infrastructure.Maps;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// The fast fog lifter reveals exactly the tiles the old one did. <see cref="VisionRevealReference"/> is
/// the old algorithm, kept as it was; both run on the sample pack's map from spots all over it, at short
/// and long ranges, with the doors shut, open, and secret.
/// </summary>
public sealed class VisionRevealEquivalenceTests
{
    private static readonly string[] None = Array.Empty<string>();

    private static readonly Lazy<VectorMap> SampleMap = new Lazy<VectorMap>(() =>
    {
        var read = new DungeonScrawlMapReader().ReadFileAsync(SamplePack.MapFile, CancellationToken.None).GetAwaiter().GetResult();
        Assert.True(read.IsValid);
        return read.Value;
    });

    /// <summary>The ranges the DM screen offers, and one that is not a multiple of a square.</summary>
    public static TheoryData<int> Ranges => new TheoryData<int> { 10, 30, 60, 120, 37 };

    [Theory]
    [MemberData(nameof(Ranges))]
    public void With_every_door_shut_the_fast_lifter_matches_the_old_one(int feet)
    {
        AssertSameFromSpots(SampleMap.Value, feet, None, None, offset: 0);
    }

    [Theory]
    [MemberData(nameof(Ranges))]
    public void With_every_door_open_the_fast_lifter_matches_the_old_one(int feet)
    {
        VectorMap map = SampleMap.Value;
        AssertSameFromSpots(map, feet, None, map.Doors.Select(d => d.Id).ToArray(), offset: 1);
    }

    [Theory]
    [MemberData(nameof(Ranges))]
    public void With_secret_doors_and_open_ones_the_fast_lifter_matches_the_old_one(int feet)
    {
        // Every other door hidden, the rest open: a secret door's segment changes which wall ends count as
        // corners, which is the subtle part of keeping the walls' corners extended exactly as before.
        VectorMap map = SampleMap.Value;
        string[] secret = map.Doors.Where((_, i) => i % 2 == 0).Select(d => d.Id).ToArray();
        string[] open = map.Doors.Where((_, i) => i % 2 == 1).Select(d => d.Id).ToArray();
        AssertSameFromSpots(map, feet, secret, open, offset: 2);
    }

    [Fact]
    public void A_viewer_off_the_floor_and_off_the_grid_sees_what_the_old_lifter_saw()
    {
        // Spots outside the grid and beyond the map's edge take the paths that do not use the rows the
        // index sorted the floor's edges into.
        VectorMap map = SampleMap.Value;
        Grid g = map.Grid;
        var spots = new[]
        {
            (g.OriginX - (3 * g.CellSizePx), g.OriginY - (3 * g.CellSizePx)),
            (g.OriginX + ((g.Cols + 2) * g.CellSizePx), g.OriginY + (g.Rows * g.CellSizePx / 2)),
            (g.OriginX + (g.Cols * g.CellSizePx / 2), g.OriginY + ((g.Rows + 1) * g.CellSizePx)),
        };

        foreach ((double x, double y) in spots)
        {
            AssertSame(map, x, y, 60, None, None);
        }
    }

    [Fact]
    public void A_secret_door_meeting_a_wall_end_closes_the_corner_as_it_did_under_the_old_lifter()
    {
        // A wall ends at (49.8, 34.9) and a secret door's blocking segment starts at (50.3, 35.2): both ends
        // round to the same whole point, so the old lifter counted them as a corner and extended both,
        // closing the hairline gap between them. The fast lifter must count the secret door's segment too,
        // or sight leaks through the gap into the tile beyond.
        var map = new VectorMap
        {
            Grid = new Grid { CellSizePx = 10, Cols = 10, Rows = 10 },
            Bounds = new MapBounds(0, 0, 100, 100),
            Layers = new[]
            {
                new MapLayer { Kind = LayerKind.Floor, Geometry = Polygon(new MapPoint(0, 0), new MapPoint(100, 0), new MapPoint(100, 100), new MapPoint(0, 100)) },
                new MapLayer
                {
                    Kind = LayerKind.Walls,
                    Geometry = new VectorGeometry
                    {
                        Polylines = new[] { new Polyline { Points = new[] { new MapPoint(0, 34.9), new MapPoint(49.8, 34.9) } } },
                    },
                },
            },
            Doors = new[]
            {
                new DoorFeature { Id = "sd", Orientation = DoorOrientation.Vertical, Bounds = new MapBounds(48.3, 40.2, 52.3, 60) },
            },
        };
        string[] secret = { "sd" };

        var seen = VisionReveal.Find(map, 45, 45, 20, secret, None);

        Assert.DoesNotContain((5, 2), seen);
        AssertSame(map, 45, 45, 20, secret, None);
    }

    [Fact]
    public void The_hand_built_maps_reveal_what_they_did_under_the_old_lifter()
    {
        // A map whose grid has no rows or columns set (the small maps the other tests build) scans every
        // floor edge for every row instead of only the edges filed under it.
        var map = new VectorMap
        {
            Grid = new Grid { CellSizePx = 10 },
            Bounds = new MapBounds(0, 0, 100, 100),
            Layers = new[]
            {
                new MapLayer { Kind = LayerKind.Floor, Geometry = Polygon(new MapPoint(0, 0), new MapPoint(100, 0), new MapPoint(100, 50), new MapPoint(50, 100), new MapPoint(0, 100)) },
                new MapLayer
                {
                    Kind = LayerKind.Walls,
                    Geometry = new VectorGeometry
                    {
                        Polylines = new[]
                        {
                            new Polyline { Points = new[] { new MapPoint(100, 50), new MapPoint(50, 100) } },
                            new Polyline { Points = new[] { new MapPoint(40, 0), new MapPoint(40, 45) } },
                            new Polyline { Points = new[] { new MapPoint(40, 55), new MapPoint(40, 100) } },
                        },
                    },
                },
            },
        };

        for (double x = 5; x < 100; x += 10)
        {
            for (double y = 5; y < 100; y += 10)
            {
                AssertSame(map, x, y, 30, None, None);
            }
        }
    }

    private static void AssertSameFromSpots(VectorMap map, int feet, string[] secret, string[] open, int offset)
    {
        // Every third floor cell along each axis, starting a cell further in for each door setting, so the
        // three settings between them stand somewhere different: in rooms, corridors and doorways alike.
        Grid g = map.Grid;
        int tested = 0;
        for (int col = offset; col < g.Cols; col += 3)
        {
            for (int row = offset; row < g.Rows; row += 3)
            {
                MapBounds b = GridCells.CellBounds(g, col, row);
                double x = (b.MinX + b.MaxX) / 2, y = (b.MinY + b.MaxY) / 2;
                if (VisionIndex.For(map).FloorMask(col, row) == 0)
                {
                    continue;
                }

                AssertSame(map, x, y, feet, secret, open);
                tested++;
            }
        }

        Assert.True(tested >= 15, $"Only {tested} spots were on the floor; the test would prove little.");
    }

    private static void AssertSame(VectorMap map, double x, double y, int feet, string[] secret, string[] open)
    {
        var expected = new HashSet<(int, int)>(VisionRevealReference.Find(map, x, y, feet, secret, open));
        var actual = VisionReveal.Find(map, x, y, feet, secret, open);

        Assert.Equal(expected.Count, actual.Count);
        Assert.All(actual, cell => Assert.Contains(cell, expected));
    }

    private static VectorGeometry Polygon(params MapPoint[] corners) =>
        new VectorGeometry
        {
            Polygons = new[]
            {
                new MapPolygon { Rings = new[] { new PolygonRing { Vertices = corners.Append(corners[0]).ToArray() } } },
            },
        };
}
