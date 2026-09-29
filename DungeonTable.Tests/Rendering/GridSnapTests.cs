using DungeonTable.Core.Maps;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// The Room Editor's two snaps: a region's corner goes to the nearest grid intersection, so an
/// outline runs along the printed grid lines, and a feature marker goes to the middle of the square
/// that was clicked, so a trap sits in its square rather than on the corner four squares share.
/// </summary>
public sealed class GridSnapTests
{
    private static readonly Grid TenFootGrid = new Grid { OriginX = 0, OriginY = 0, CellSizePx = 10 };

    [Fact]
    public void A_marker_lands_in_the_middle_of_the_square_that_was_clicked()
    {
        Assert.Equal(new MapPoint(25, 35), GridCells.CellCentre(TenFootGrid, 23, 37));
        Assert.Equal(new MapPoint(25, 35), GridCells.CellCentre(TenFootGrid, 20.5, 39.5));
    }

    [Fact]
    public void A_click_beside_a_corner_stays_in_its_own_square()
    {
        // The corner at (20, 20) is nearer than any centre, which is where the old snap put a marker.
        Assert.Equal(new MapPoint(15, 25), GridCells.CellCentre(TenFootGrid, 19.9, 20.1));
        Assert.Equal(new MapPoint(25, 15), GridCells.CellCentre(TenFootGrid, 20.1, 19.9));
    }

    [Fact]
    public void A_region_corner_lands_on_the_nearest_grid_intersection()
    {
        Assert.Equal(new MapPoint(20, 40), GridCells.NearestCorner(TenFootGrid, 23, 37));
        Assert.Equal(new MapPoint(30, 30), GridCells.NearestCorner(TenFootGrid, 26, 34));
    }

    [Fact]
    public void Both_snaps_follow_the_grid_origin()
    {
        var offset = new Grid { OriginX = 5, OriginY = 5, CellSizePx = 10 };

        Assert.Equal(new MapPoint(10, 10), GridCells.CellCentre(offset, 9, 9));
        Assert.Equal(new MapPoint(0, 0), GridCells.CellCentre(offset, 4, 4));
        Assert.Equal(new MapPoint(5, 5), GridCells.NearestCorner(offset, 9, 9));
        Assert.Equal(new MapPoint(15, 15), GridCells.NearestCorner(offset, 12, 12));
    }

    [Fact]
    public void A_map_with_no_grid_leaves_the_point_where_it_was()
    {
        var none = new Grid { CellSizePx = 0 };

        Assert.Equal(new MapPoint(23.4, 37.8), GridCells.CellCentre(none, 23.4, 37.8));
        Assert.Equal(new MapPoint(23.4, 37.8), GridCells.NearestCorner(none, 23.4, 37.8));
    }

    [Fact]
    public void A_marker_already_in_the_middle_of_its_square_stays_put()
    {
        // The sample map's grid: 36 world units a square. The trap the pack places by hand is where
        // the snap puts it, and snapping it again moves nothing.
        var undercroft = new Grid { CellSizePx = 36 };

        Assert.Equal(new MapPoint(1422, 882), GridCells.CellCentre(undercroft, 1422, 882));
        Assert.Equal(new MapPoint(1422, 882), GridCells.CellCentre(undercroft, 1407, 899));
    }
}
