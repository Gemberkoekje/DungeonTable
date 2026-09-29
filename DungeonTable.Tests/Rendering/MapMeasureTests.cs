using DungeonTable.Core.Maps;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// Verifies the map's measuring tool reports the distance a DM counting squares on the drawn grid
/// would get: 5e's Chebyshev rule for the rules figure, true geometry for the straight-line one.
/// </summary>
public sealed class MapMeasureTests
{
    // A 10-unit grid whose origin is deliberately NOT 0: an origin-blind implementation happens to
    // pass every case on a zero-origin grid, and the real maps' grids are offset.
    private static readonly Grid Grid10 = new Grid { CellSizePx = 10, OriginX = 3, OriginY = 7 };

    [Fact]
    public void Counts_straight_runs_in_squares_of_five_feet()
    {
        // From the middle of one cell to the middle of the cell four columns along.
        MapMeasurement measured = MapMeasure.Between(Grid10, 8, 12, 48, 12);

        Assert.True(measured.IsSet);
        Assert.True(measured.HasGrid);
        Assert.Equal(4, measured.Squares);
        Assert.Equal(20, measured.Feet);
        Assert.Equal(20, measured.StraightFeet);
    }

    [Fact]
    public void A_diagonal_costs_the_same_as_a_straight_step()
    {
        // Four across and four down is four squares in 5e, not eight and not five-and-a-bit — and
        // the straight-line figure is the one that says how far it really is.
        MapMeasurement measured = MapMeasure.Between(Grid10, 8, 12, 48, 52);

        Assert.Equal(4, measured.Squares);
        Assert.Equal(20, measured.Feet);
        Assert.Equal(28, measured.StraightFeet);
    }

    [Fact]
    public void Counts_between_the_cells_the_points_fall_in_not_the_raw_delta()
    {
        // Both ends sit inside the same square, a hair either side of its centre: the raw delta is
        // most of a cell, but a DM counting squares reads zero, and so must this.
        MapMeasurement measured = MapMeasure.Between(Grid10, 4, 8, 12, 16);

        Assert.Equal(0, measured.Squares);
        Assert.Equal(0, measured.Feet);
    }

    [Fact]
    public void A_step_across_a_cell_boundary_is_one_square_however_short_it_is()
    {
        // Two points a whisker apart but on either side of a grid line really are one square apart
        // under the rule, and that is the answer that settles whether a reach works.
        MapMeasurement measured = MapMeasure.Between(Grid10, 12.9, 12, 13.1, 12);

        Assert.Equal(1, measured.Squares);
        Assert.Equal(5, measured.Feet);
        Assert.Equal(0, measured.StraightFeet);
    }

    [Fact]
    public void An_uncalibrated_map_reports_no_square_count_rather_than_a_made_up_one()
    {
        MapMeasurement measured = MapMeasure.Between(new Grid(), 0, 0, 100, 0);

        Assert.True(measured.IsSet);
        Assert.False(measured.HasGrid);
        Assert.Equal(0, measured.Squares);
        Assert.Equal(0, measured.Feet);
        Assert.Equal(string.Empty, MapMeasurement.None.ShortLabel);
    }

    [Fact]
    public void Non_finite_input_measures_nothing()
    {
        Assert.False(MapMeasure.Between(Grid10, double.NaN, 0, 10, 10).IsSet);
        Assert.False(MapMeasure.Between(Grid10, 0, 0, double.PositiveInfinity, 10).IsSet);
    }

    [Fact]
    public void The_map_label_is_the_rules_figure_when_there_is_a_grid()
    {
        Assert.Equal("20 ft", MapMeasure.Between(Grid10, 8, 12, 48, 52).ShortLabel);

        // Without a grid the figure is an estimate off the geometry, and says so.
        Assert.StartsWith("~", MapMeasure.Between(new Grid(), 0, 0, 100, 0).ShortLabel, StringComparison.Ordinal);
    }
}
