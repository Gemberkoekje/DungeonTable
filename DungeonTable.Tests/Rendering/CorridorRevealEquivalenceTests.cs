using System.Collections.Generic;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// The fast corridor peek reaches exactly the tiles the old one did, each at the same distance.
/// <see cref="CorridorRevealReference"/> is the old algorithm, kept as it was; both run on the sample
/// pack's map from reveals all over it, with the doors shut, open, and secret.
/// </summary>
public sealed class CorridorRevealEquivalenceTests
{
    private static readonly string[] None = Array.Empty<string>();

    /// <summary>The door settings: all shut, all open, and every other one secret with the rest open.</summary>
    public static TheoryData<string> Doors => new TheoryData<string> { "shut", "open", "secret" };

    [Theory]
    [MemberData(nameof(Doors))]
    public void From_lifted_cells_the_fast_peek_matches_the_old_one(string doors)
    {
        VectorMap map = SampleMap.Map;
        (string[] secret, string[] open) = Setting(doors);
        foreach ((int col, int row) in FloorSpots(map, every: 2))
        {
            MapBounds b = GridCells.CellBounds(map.Grid, col, row);
            var cells = VisionReveal.Find(map, (b.MinX + b.MaxX) / 2, (b.MinY + b.MaxY) / 2, 20, secret, open)
                .Select(c => GridCells.CellBounds(map.Grid, c.Col, c.Row)).ToList();

            AssertSame(map, Array.Empty<IReadOnlyList<MapPoint>>(), cells, secret, open);
        }
    }

    [Theory]
    [MemberData(nameof(Doors))]
    public void From_revealed_rooms_the_fast_peek_matches_the_old_one(string doors)
    {
        (string[] secret, string[] open) = Setting(doors);
        foreach (Region region in SampleMap.Notes.Regions)
        {
            AssertSame(SampleMap.Map, new[] { region.Polygon }, Array.Empty<MapBounds>(), secret, open);
        }
    }

    [Fact]
    public void On_a_map_whose_grid_has_no_size_the_fast_peek_matches_the_old_one()
    {
        // The hand-built maps of the other tests set no columns or rows, so no floor edge is filed by row.
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
                                Rings = new[] { new PolygonRing { Vertices = new[] { new MapPoint(0, 0), new MapPoint(100, 0), new MapPoint(100, 100), new MapPoint(0, 100), new MapPoint(0, 0) } } },
                            },
                        },
                    },
                },
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
        };

        for (int col = 0; col < 10; col += 3)
        {
            AssertSame(map, Array.Empty<IReadOnlyList<MapPoint>>(), new[] { GridCells.CellBounds(map.Grid, col, 4) }, None, None);
        }
    }

    [Fact]
    public void A_wall_standing_inside_the_next_cell_stops_the_peek_as_it_did()
    {
        // A short wall wholly inside cell (5,4), across the line from (4,4)'s centre to (5,4)'s: it touches
        // only the cell the step goes into, so the walls near BOTH cells have to be tried.
        var map = new VectorMap
        {
            Grid = new Grid { CellSizePx = 10, Cols = 10, Rows = 10 },
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
                                Rings = new[] { new PolygonRing { Vertices = new[] { new MapPoint(0, 0), new MapPoint(100, 0), new MapPoint(100, 100), new MapPoint(0, 100), new MapPoint(0, 0) } } },
                            },
                        },
                    },
                },
                new MapLayer
                {
                    Kind = LayerKind.Walls,
                    Geometry = new VectorGeometry
                    {
                        Polylines = new[] { new Polyline { Points = new[] { new MapPoint(52, 42), new MapPoint(52, 48) } } },
                    },
                },
            },
        };
        var start = new[] { GridCells.CellBounds(map.Grid, 4, 4) };

        var reached = CorridorReveal.Find(map, Array.Empty<IReadOnlyList<MapPoint>>(), start, 1, None, None);

        Assert.DoesNotContain(reached, step => step.Cell == GridCells.CellBounds(map.Grid, 5, 4));
        AssertSame(map, Array.Empty<IReadOnlyList<MapPoint>>(), start, None, None);
    }

    private static (string[] Secret, string[] Open) Setting(string doors)
    {
        string[] all = SampleMap.Map.Doors.Select(d => d.Id).ToArray();
        return doors switch
        {
            "open" => (None, all),
            "secret" => (all.Where((_, i) => i % 2 == 0).ToArray(), all.Where((_, i) => i % 2 == 1).ToArray()),
            _ => (None, None),
        };
    }

    private static IEnumerable<(int Col, int Row)> FloorSpots(VectorMap map, int every)
    {
        for (int col = 0; col < map.Grid.Cols; col += every)
        {
            for (int row = 0; row < map.Grid.Rows; row += every)
            {
                if (VisionIndex.For(map).FloorMask(col, row) != 0)
                {
                    yield return (col, row);
                }
            }
        }
    }

    private static void AssertSame(
        VectorMap map, IReadOnlyList<IReadOnlyList<MapPoint>> regions, IReadOnlyList<MapBounds> cells, string[] secret, string[] open)
    {
        var expected = CorridorRevealReference.Find(map, regions, cells, 2, secret, open).ToHashSet();
        var actual = CorridorReveal.Find(map, regions, cells, 2, secret, open);

        Assert.Equal(expected.Count, actual.Count);
        Assert.All(actual, step => Assert.Contains(step, expected));
    }
}
