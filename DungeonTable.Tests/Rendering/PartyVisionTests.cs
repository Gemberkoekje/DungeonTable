using System.Collections.Generic;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// The squares a walk passes through, filled in square by square, and what the party sees from the ones
/// with floor in them: a viewer in solid rock would see into every room around it.
/// </summary>
public sealed class PartyVisionTests
{
    private static readonly (int Col, int Row)[] FiveAlongRowZero = { (0, 0), (1, 0), (2, 0), (3, 0), (4, 0) };
    private static readonly (int Col, int Row)[] RockSquare = { (4, 1) };
    private static readonly (int Col, int Row)[] EitherRoom = { (1, 1), (7, 1) };
    private static readonly string[] NoDoors = System.Array.Empty<string>();

    private static readonly Grid TenUnitGrid = new Grid { CellSizePx = 10 };

    [Fact]
    public void A_walk_passes_every_square_on_the_way_once_and_in_order()
    {
        IReadOnlyList<(int Col, int Row)> path = PartyVision.Path(TenUnitGrid, new MapPoint(5, 5), new MapPoint(45, 5));

        Assert.Equal(FiveAlongRowZero, path);
    }

    [Fact]
    public void A_diagonal_walk_crosses_squares_in_order_from_start_to_end()
    {
        IReadOnlyList<(int Col, int Row)> path = PartyVision.Path(TenUnitGrid, new MapPoint(5, 5), new MapPoint(35, 25));

        Assert.Equal((0, 0), path[0]);
        Assert.Equal((3, 2), path[^1]);
        Assert.Equal(path.Count, path.Distinct().Count());
        for (int i = 1; i < path.Count; i++)
        {
            // Each square touches the one before it: none is skipped, however fast the drag.
            Assert.True(Math.Abs(path[i].Col - path[i - 1].Col) <= 1 && Math.Abs(path[i].Row - path[i - 1].Row) <= 1);
        }
    }

    [Fact]
    public void Standing_still_is_one_square()
    {
        Assert.Equal((2, 3), Assert.Single(PartyVision.Path(TenUnitGrid, new MapPoint(25, 35), new MapPoint(25, 35))));
    }

    [Fact]
    public void A_map_with_no_grid_has_no_squares_to_walk()
    {
        Assert.Empty(PartyVision.Path(new Grid(), new MapPoint(5, 5), new MapPoint(45, 5)));
    }

    [Fact]
    public void Nothing_is_seen_from_a_square_of_solid_rock()
    {
        // Two rooms, columns 0-2 and 6-8, with rock between: from column 4 a 10 ft range reaches both.
        IReadOnlyList<(int Col, int Row)> seen = PartyVision.SeenFrom(TwoRoomsAndRock(), RockSquare, 10, NoDoors, NoDoors);

        Assert.Empty(seen);
    }

    [Fact]
    public void What_each_square_sees_is_put_together_once()
    {
        IReadOnlyList<(int Col, int Row)> seen = PartyVision.SeenFrom(TwoRoomsAndRock(), EitherRoom, 10, NoDoors, NoDoors);

        Assert.Contains((0, 0), seen);
        Assert.Contains((8, 2), seen);
        Assert.DoesNotContain((4, 1), seen);
        Assert.Equal(seen.Count, seen.Distinct().Count());
    }

    private static VectorMap TwoRoomsAndRock() =>
        new VectorMap
        {
            Grid = TenUnitGrid,
            Bounds = new MapBounds(0, 0, 90, 30),
            Layers = new[]
            {
                new MapLayer
                {
                    Kind = LayerKind.Floor,
                    Geometry = new VectorGeometry { Polygons = new[] { Square(0, 0, 30, 30), Square(60, 0, 90, 30) } },
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
}
