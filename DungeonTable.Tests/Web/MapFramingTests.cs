using System.Collections.Generic;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Verifies the world-unit framing helpers behind the shell's "Frame" button, the Info tab's
/// orientation mini map, and the status bar's "which region is the projector pointed at" readout.
/// </summary>
public sealed class MapFramingTests
{
    private static readonly MapBounds Map = new MapBounds(0, 0, 1000, 800);

    [Fact]
    public void Bounds_unions_every_region_polygon()
    {
        MapBounds bounds = MapFraming.Bounds(new[] { Rect("a", 10, 20, 50, 60), Rect("b", 100, 5, 120, 40) });

        Assert.Equal(new MapBounds(10, 5, 120, 60), bounds);
    }

    [Fact]
    public void Bounds_of_nothing_is_empty()
    {
        Assert.True(MapFraming.Bounds(Array.Empty<Region>()).IsEmpty);

        // A region with no vertices contributes nothing, so the result is still empty.
        Assert.True(MapFraming.Bounds(new[] { new Region { RegionId = "empty" } }).IsEmpty);
    }

    [Fact]
    public void Padding_grows_every_side_and_ignores_a_non_positive_margin()
    {
        var bounds = new MapBounds(10, 10, 20, 20);

        Assert.Equal(new MapBounds(8, 8, 22, 22), MapFraming.Pad(bounds, 2));
        Assert.Equal(bounds, MapFraming.Pad(bounds, 0));
        Assert.Equal(bounds, MapFraming.Pad(bounds, -5));
    }

    [Fact]
    public void At_least_grows_a_tiny_rectangle_around_its_centre_and_leaves_a_big_one_alone()
    {
        MapBounds grown = MapFraming.AtLeast(new MapBounds(100, 100, 104, 104), 10);

        Assert.Equal(new MapBounds(97, 97, 107, 107), grown);
        Assert.Equal(new MapBounds(0, 0, 50, 50), MapFraming.AtLeast(new MapBounds(0, 0, 50, 50), 10));
    }

    [Fact]
    public void Framing_a_tall_room_widens_it_to_the_projector_aspect_and_keeps_it_centred()
    {
        // A 100x200 room mid-map at 2:1 must become 400x200, still centred on (450, 200).
        MapBounds framed = MapFraming.Frame(new MapBounds(400, 100, 500, 300), 2.0, Map);

        Assert.Equal(400, framed.Width, 6);
        Assert.Equal(200, framed.Height, 6);
        Assert.Equal(450, framed.MinX + (framed.Width / 2), 6);
        Assert.Equal(200, framed.MinY + (framed.Height / 2), 6);
    }

    [Fact]
    public void Framing_stays_inside_the_map_even_at_its_edge()
    {
        MapBounds framed = MapFraming.Frame(new MapBounds(960, 760, 1000, 800), 2.0, Map);

        Assert.True(framed.MinX >= Map.MinX);
        Assert.True(framed.MinY >= Map.MinY);
        Assert.True(framed.MaxX <= Map.MaxX + 1e-9);
        Assert.True(framed.MaxY <= Map.MaxY + 1e-9);
        Assert.Equal(2.0, framed.Width / framed.Height, 6);
    }

    [Fact]
    public void Framing_a_room_bigger_than_the_map_shrinks_to_fit_and_keeps_the_aspect()
    {
        MapBounds framed = MapFraming.Frame(new MapBounds(-500, -500, 2000, 2000), 2.0, Map);

        Assert.Equal(1000, framed.Width, 6);
        Assert.Equal(500, framed.Height, 6);
    }

    [Fact]
    public void Framing_nothing_falls_back_to_the_whole_map()
    {
        Assert.Equal(MapFraming.DefaultViewport(Map, 2.0), MapFraming.Frame(default, 2.0, Map));
    }

    [Fact]
    public void A_bogus_aspect_falls_back_to_widescreen_rather_than_degenerating()
    {
        MapBounds framed = MapFraming.Frame(new MapBounds(100, 100, 200, 200), 0, Map);

        Assert.Equal(16.0 / 9.0, framed.Width / framed.Height, 6);
    }

    [Fact]
    public void Clamping_shrinks_an_oversized_viewport_and_slides_an_escaping_one_back()
    {
        Assert.Equal(new MapBounds(0, 0, 1000, 800), MapFraming.ClampToMap(new MapBounds(-100, -100, 1500, 1500), Map));
        Assert.Equal(new MapBounds(900, 700, 1000, 800), MapFraming.ClampToMap(new MapBounds(950, 750, 1050, 850), Map));

        // An empty map cannot constrain anything, so the viewport passes through untouched.
        Assert.Equal(new MapBounds(5, 5, 10, 10), MapFraming.ClampToMap(new MapBounds(5, 5, 10, 10), default));
    }

    [Fact]
    public void The_default_viewport_is_the_widest_rectangle_of_the_aspect_that_fits()
    {
        // 1000x800 at 2:1 is limited by width: 1000x500, vertically centred.
        MapBounds wide = MapFraming.DefaultViewport(Map, 2.0);
        Assert.Equal(new MapBounds(0, 150, 1000, 650), wide);

        // At 1:1 it is limited by height instead: 800x800, horizontally centred.
        MapBounds square = MapFraming.DefaultViewport(Map, 1.0);
        Assert.Equal(new MapBounds(100, 0, 900, 800), square);
    }

    [Fact]
    public void Region_at_finds_the_polygon_under_a_point_and_nothing_outside_one()
    {
        var regions = new List<Region> { Rect("a", 0, 0, 100, 100), Rect("b", 200, 200, 300, 300) };

        Assert.Equal("a", MapFraming.RegionAt(regions, 50, 50));
        Assert.Equal("b", MapFraming.RegionAt(regions, 250, 250));
        Assert.Equal(string.Empty, MapFraming.RegionAt(regions, 150, 150));
    }

    [Fact]
    public void A_degenerate_region_is_never_hit()
    {
        var regions = new List<Region>
        {
            new Region { RegionId = "line", Polygon = new[] { new MapPoint(0, 0), new MapPoint(10, 10) } },
        };

        Assert.Equal(string.Empty, MapFraming.RegionAt(regions, 5, 5));
    }

    private static Region Rect(string id, double minX, double minY, double maxX, double maxY) => new Region
    {
        RegionId = id,
        Polygon = new[]
        {
            new MapPoint(minX, minY),
            new MapPoint(maxX, minY),
            new MapPoint(maxX, maxY),
            new MapPoint(minX, maxY),
        },
    };
}
