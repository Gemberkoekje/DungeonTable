using DungeonTable.Core.Maps;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// Verifies the sized fog brush's footprint: which grid cells one stroke of an N×N brush covers.
/// </summary>
public sealed class FogBrushTests
{
    private static readonly Grid TenFootGrid = new Grid { OriginX = 0, OriginY = 0, CellSizePx = 10 };

    [Fact]
    public void A_one_cell_brush_paints_only_the_cell_under_the_pointer()
    {
        var cells = GridCells.BrushCells(TenFootGrid, 25, 35, 1);

        Assert.Equal((2, 3), Assert.Single(cells));
    }

    [Fact]
    public void A_brush_size_below_one_still_paints_a_single_cell()
    {
        Assert.Equal((2, 3), Assert.Single(GridCells.BrushCells(TenFootGrid, 25, 35, 0)));
        Assert.Equal((2, 3), Assert.Single(GridCells.BrushCells(TenFootGrid, 25, 35, -4)));
    }

    [Fact]
    public void A_three_cell_brush_paints_the_block_centred_on_the_pointer()
    {
        var cells = GridCells.BrushCells(TenFootGrid, 25, 35, 3);

        Assert.Equal(9, cells.Count);
        Assert.Contains((2, 3), cells);   // the pointer's own cell
        Assert.Contains((1, 2), cells);   // top-left corner of the block
        Assert.Contains((3, 4), cells);   // bottom-right corner
        Assert.DoesNotContain((0, 3), cells);
    }

    [Fact]
    public void An_even_brush_is_anchored_at_the_pointers_own_cell()
    {
        // Two cells wide has no true centre, so it extends right and down from the pointer.
        var cells = GridCells.BrushCells(TenFootGrid, 25, 35, 2);

        Assert.Equal(4, cells.Count);
        Assert.Contains((2, 3), cells);
        Assert.Contains((3, 4), cells);
        Assert.DoesNotContain((1, 2), cells);
    }

    [Fact]
    public void The_footprint_follows_the_grid_origin()
    {
        var offset = new Grid { OriginX = 5, OriginY = 5, CellSizePx = 10 };

        Assert.Equal((0, 0), Assert.Single(GridCells.BrushCells(offset, 9, 9, 1)));
        Assert.Equal((-1, -1), Assert.Single(GridCells.BrushCells(offset, 4, 4, 1)));
    }
}
