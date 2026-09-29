using DungeonTable.Core.Briefing;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Core.Session;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// Verifies that <see cref="VectorMapSvgRenderer"/> draws the editor overlay (regions, markers,
/// object outlines and the draft polygon) with the id-tagged hooks the editor hit-tests, in the
/// map's own world coordinates, and only when an overlay is supplied.
/// </summary>
public sealed class EditorOverlayRenderTests
{
    private static VectorMap BareMap() =>
        new VectorMap { Bounds = new MapBounds(0, 0, 100, 100), Grid = new Grid { CellSizePx = 10 } };

    private static EditorOverlay SampleOverlay() =>
        new EditorOverlay
        {
            Regions = new[]
            {
                new Region
                {
                    RegionId = "r1",
                    Label = "Guard room",
                    Polygon = new[] { new MapPoint(10, 10), new MapPoint(20, 10), new MapPoint(20, 20) },
                },
            },
            Markers = new[]
            {
                new FeatureMarker { FeatureId = "f1", Kind = FeatureKind.Trap, Position = new MapPoint(30, 30) },
            },
            Objects = new[]
            {
                new ObjectHighlight
                {
                    Id = "door-1",
                    Kind = MapObjectKind.Door,
                    Bounds = new MapBounds(40, 40, 48, 52),
                    IsSecret = true,
                },
            },
            HitTargets = new[]
            {
                new ObjectHighlight { Id = "door-9", Kind = MapObjectKind.Door, Bounds = new MapBounds(60, 60, 68, 72) },
            },
            SelectedRegionId = "r1",
            UnitSize = 10,
        };

    [Fact]
    public void Overlay_is_absent_by_default()
    {
        string svg = VectorMapSvgRenderer.Render(BareMap(), VectorMapRenderOptions.Default);

        Assert.DoesNotContain("editor-overlay", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlay_draws_region_with_hit_hook_and_world_points()
    {
        string svg = Render();

        Assert.Contains("class=\"editor-overlay\"", svg, StringComparison.Ordinal);
        Assert.Contains("data-region-id=\"r1\"", svg, StringComparison.Ordinal);
        Assert.Contains("<polygon", svg, StringComparison.Ordinal);
        // Region vertices are emitted in world units, not re-projected.
        Assert.Contains("10,10", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlay_draws_marker_glyph_with_hit_hook()
    {
        string svg = Render();

        Assert.Contains("data-feature-id=\"f1\"", svg, StringComparison.Ordinal);
        Assert.Contains(">T</text>", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlay_draws_secret_object_outline_dashed()
    {
        string svg = Render();

        Assert.Contains("<rect", svg, StringComparison.Ordinal);
        Assert.Contains("stroke-dasharray", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlay_draws_the_region_label()
    {
        string svg = Render();

        Assert.Contains(">Guard room</text>", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlay_draws_transparent_object_hit_targets_last()
    {
        string svg = Render();

        // A door/stair stays clickable over a region fill via a transparent hit-target group.
        Assert.Contains("data-object-id=\"door-9\"", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"transparent\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlay_draws_the_measured_line_with_its_distance()
    {
        string svg = RenderWith(new EditorOverlay
        {
            UnitSize = 10,
            Measure = MapMeasure.Between(BareMap().Grid, 10, 10, 50, 10),
        });

        Assert.Contains("measure-line", svg, StringComparison.Ordinal);
        Assert.Contains(">20 ft</text>", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Overlay_marks_where_the_dm_last_pointed()
    {
        string svg = RenderWith(new EditorOverlay
        {
            UnitSize = 10,
            Ping = new PingMarker { X = 40, Y = 60, Sequence = 3 },
        });

        Assert.Contains("ping-mark", svg, StringComparison.Ordinal);
        Assert.Contains("cx=\"40\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Neither_is_drawn_when_the_dm_has_not_used_them()
    {
        // An overlay carrying only regions must not gain a stray line or dot — both are DM working
        // notes, and one left lying across the map would read as part of the dungeon.
        string svg = Render();

        Assert.DoesNotContain("measure-line", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("ping-mark", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void An_overlay_holding_only_a_measurement_still_draws()
    {
        // IsEmpty short-circuits the whole overlay pass, so a measurement taken on a map with no
        // regions at all would silently render nothing if it did not count as content.
        var overlay = new EditorOverlay
        {
            UnitSize = 10,
            Measure = MapMeasure.Between(BareMap().Grid, 0, 0, 30, 0),
        };

        Assert.False(overlay.IsEmpty);
        Assert.Contains("measure-line", RenderWith(overlay), StringComparison.Ordinal);
    }

    [Fact]
    public void A_region_outline_in_progress_is_drawn_click_through_with_its_last_corner_for_the_pointer()
    {
        string svg = RenderWith(new EditorOverlay
        {
            UnitSize = 10,
            Draft = new[] { new MapPoint(10, 10), new MapPoint(40, 10), new MapPoint(40, 30) },
            DraftSnaps = true,
        });

        // The next click must land on the map beneath, not on the outline it extends.
        Assert.Contains("<g class=\"dt-draft-poly\" pointer-events=\"none\"", svg, StringComparison.Ordinal);
        Assert.Contains("<polyline points=\"10,10 40,10 40,30\"", svg, StringComparison.Ordinal);

        // dt-editor.js draws the segment to the pointer from here, snapped as the click would be.
        Assert.Contains("data-last-x=\"40\" data-last-y=\"30\" data-snap=\"true\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void The_first_corner_of_an_outline_is_marked_larger_because_clicking_it_closes_the_outline()
    {
        string svg = RenderWith(new EditorOverlay
        {
            UnitSize = 10,
            Draft = new[] { new MapPoint(10, 10), new MapPoint(40, 10) },
        });

        Assert.Contains("<circle cx=\"10\" cy=\"10\" r=\"3.4\"", svg, StringComparison.Ordinal);
        Assert.Contains("<circle cx=\"40\" cy=\"10\" r=\"2\"", svg, StringComparison.Ordinal);
        Assert.Contains("data-snap=\"false\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void An_outline_of_one_corner_is_that_corner_alone()
    {
        var overlay = new EditorOverlay { UnitSize = 10, Draft = new[] { new MapPoint(10, 10) } };

        string svg = RenderWith(overlay);

        Assert.False(overlay.IsEmpty);
        Assert.Contains("dt-draft-poly", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<polyline", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void No_outline_is_drawn_when_none_is_being_drawn_or_its_corners_are_not_points()
    {
        Assert.DoesNotContain("dt-draft-poly", Render(), StringComparison.Ordinal);
        Assert.DoesNotContain("dt-draft-poly", RenderWith(new EditorOverlay
        {
            UnitSize = 10,
            Draft = new[] { new MapPoint(10, 10), new MapPoint(double.NaN, 10) },
        }), StringComparison.Ordinal);
    }

    private static string Render() => RenderWith(SampleOverlay());

    private static string RenderWith(EditorOverlay overlay) =>
        VectorMapSvgRenderer.Render(BareMap(), new VectorMapRenderOptions { Overlay = overlay });
}
