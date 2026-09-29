using System.Collections.Generic;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// Verifies the fog-of-war render pass: the reveal mask and fog sheet are emitted, revealed regions
/// and painted cells punch holes into it, revealed markers and secret doors are drawn, and a map
/// with no fog overlay stays completely unfogged.
/// </summary>
public sealed class FogRenderTests
{
    private const string DoorId = "door-1";

    private static VectorMap SimpleMap() =>
        new VectorMap
        {
            Bounds = new MapBounds(0, 0, 100, 100),
            Grid = new Grid { CellSizePx = 10 },
        };

    private static VectorMap MapWithSecretDoor() =>
        new VectorMap
        {
            Bounds = new MapBounds(0, 0, 100, 100),
            Grid = new Grid { CellSizePx = 10 },
            Doors = new[]
            {
                new DoorFeature
                {
                    Id = DoorId,
                    Bounds = new MapBounds(40, 48, 60, 52),
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
                                            new MapPoint(40, 48), new MapPoint(60, 48),
                                            new MapPoint(60, 52), new MapPoint(40, 52), new MapPoint(40, 48),
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

    [Fact]
    public void No_fog_overlay_leaves_the_map_unfogged()
    {
        string svg = VectorMapSvgRenderer.Render(SimpleMap(), VectorMapRenderOptions.Default);

        Assert.DoesNotContain("id=\"dt-reveal\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Enabled_fog_emits_the_reveal_mask_and_a_masked_sheet()
    {
        var options = new VectorMapRenderOptions
        {
            Fog = new FogOverlay { Enabled = true, FogColour = "#05060a", FogOpacity = 1 },
        };

        string svg = VectorMapSvgRenderer.Render(SimpleMap(), options);

        Assert.Contains("<mask id=\"dt-reveal\"", svg, StringComparison.Ordinal);
        Assert.Contains("mask=\"url(#dt-reveal)\"", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"#05060a\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void A_revealed_region_punches_a_black_hole_into_the_mask()
    {
        var polygon = new List<MapPoint>
        {
            new MapPoint(10, 10), new MapPoint(30, 10), new MapPoint(30, 30), new MapPoint(10, 30),
        };
        var options = new VectorMapRenderOptions
        {
            Fog = new FogOverlay
            {
                Enabled = true,
                RevealedPolygons = new IReadOnlyList<MapPoint>[] { polygon },
            },
        };

        string svg = VectorMapSvgRenderer.Render(SimpleMap(), options);

        Assert.Contains("<polygon points=\"10,10 30,10 30,30 10,30\" fill=\"black\"/>", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void A_revealed_cell_punches_a_black_rect_into_the_mask()
    {
        var options = new VectorMapRenderOptions
        {
            Fog = new FogOverlay
            {
                Enabled = true,
                RevealedCells = new[] { new MapBounds(20, 20, 30, 30) },
            },
        };

        string svg = VectorMapSvgRenderer.Render(SimpleMap(), options);

        Assert.Contains("<rect x=\"20\" y=\"20\" width=\"10\" height=\"10\" fill=\"black\"/>", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void A_revealed_marker_is_drawn_over_the_fog()
    {
        var marker = new FeatureMarker
        {
            FeatureId = "trap-1",
            Kind = FeatureKind.Trap,
            Position = new MapPoint(50, 50),
        };
        var options = new VectorMapRenderOptions
        {
            Fog = new FogOverlay
            {
                Enabled = true,
                RevealedMarkers = new[] { marker },
                UnitSize = 10,
            },
        };

        string svg = VectorMapSvgRenderer.Render(SimpleMap(), options);

        Assert.Contains("data-feature-id=\"trap-1\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void A_reveal_polygon_with_a_non_finite_vertex_is_skipped_not_pinned_to_origin()
    {
        var polygon = new List<MapPoint>
        {
            new MapPoint(10, 10), new MapPoint(double.NaN, 10), new MapPoint(30, 30), new MapPoint(10, 30),
        };
        var options = new VectorMapRenderOptions
        {
            Fog = new FogOverlay
            {
                Enabled = true,
                RevealedPolygons = new IReadOnlyList<MapPoint>[] { polygon },
            },
        };

        string svg = VectorMapSvgRenderer.Render(SimpleMap(), options);

        // Fail closed: the malformed reveal is omitted (no black hole), leaving the area fogged,
        // rather than coercing the bad vertex to (0,0) and punching a hole to the map origin.
        Assert.DoesNotContain("fill=\"black\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Fog_colour_is_attribute_escaped()
    {
        var options = new VectorMapRenderOptions
        {
            Fog = new FogOverlay { Enabled = true, FogColour = "\"><script/>" },
        };

        string svg = VectorMapSvgRenderer.Render(SimpleMap(), options);

        Assert.DoesNotContain("fill=\"\"><script/>\"", svg, StringComparison.Ordinal);
        Assert.Contains("&quot;&gt;&lt;script/&gt;", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void A_revealed_secret_door_renders_as_a_normal_door()
    {
        // Secret on the player view, but its id has been revealed → it should draw as a real door
        // (white slab + object hook), no longer as an anonymous wall.
        var options = new VectorMapRenderOptions
        {
            Doors = new DoorRenderState { SecretDoorIds = new[] { DoorId }, DiscoveredDoorIds = new[] { DoorId } },
            MarkSecretDoors = false,
        };

        string svg = VectorMapSvgRenderer.Render(MapWithSecretDoor(), options);

        Assert.Contains("data-object-id=\"door-1\"", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"#FFFFFF\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unrevealed_secret_door_stays_an_anonymous_wall()
    {
        var options = new VectorMapRenderOptions
        {
            Doors = new DoorRenderState { SecretDoorIds = new[] { DoorId } },
            MarkSecretDoors = false,
        };

        string svg = VectorMapSvgRenderer.Render(MapWithSecretDoor(), options);

        Assert.DoesNotContain("data-object-id=\"door-1\"", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("fill=\"#FFFFFF\"", svg, StringComparison.Ordinal);
    }
}
