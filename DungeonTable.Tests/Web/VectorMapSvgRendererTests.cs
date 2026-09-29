using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Infrastructure.Maps;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Verifies the SVG renderer turns the sample pack's vector map into the expected book-style
/// document: a bounds-derived viewBox, floor/wall/grid layers and the door/stair object pass.
/// </summary>
public sealed class VectorMapSvgRendererTests
{
    private static async Task<VectorMap> Sample()
    {
        var result = await new DungeonScrawlMapReader().ReadFileAsync(SamplePack.MapFile, CancellationToken.None);
        Assert.True(result.IsValid);
        return result.Value;
    }

    private static int Count(string haystack, string needle) =>
        Regex.Count(haystack, Regex.Escape(needle));

    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    // The renderer's viewBox is the geometry bounds padded by 24 on every side.
    private static string PaddedViewBox(MapBounds b) =>
        $"viewBox=\"{N(b.MinX - 24)} {N(b.MinY - 24)} {N(b.Width + 48)} {N(b.Height + 48)}\"";

    [Fact]
    public async Task Renders_a_single_svg_with_a_padded_bounds_viewbox()
    {
        VectorMap map = await Sample();
        string svg = VectorMapSvgRenderer.Render(map, VectorMapRenderOptions.Default);

        Assert.StartsWith("<svg", svg);
        Assert.EndsWith("</svg>", svg);
        Assert.Contains(PaddedViewBox(map.Bounds), svg);
    }

    [Fact]
    public async Task Draws_floor_fill_and_black_walls_from_the_layer_styles()
    {
        string svg = VectorMapSvgRenderer.Render(await Sample(), VectorMapRenderOptions.Default);

        Assert.Contains("fill=\"#F0ECE0\" fill-rule=\"evenodd\"", svg);
        Assert.Contains("stroke=\"#000000\" stroke-width=\"5\"", svg);
    }

    [Fact]
    public async Task Clips_the_grid_to_the_floor()
    {
        string svg = VectorMapSvgRenderer.Render(await Sample(), VectorMapRenderOptions.Default);

        Assert.Contains("<clipPath id=\"dt-floor\"", svg);
        Assert.Contains("clip-path=\"url(#dt-floor)\"", svg);
        Assert.Contains("<line ", svg);
    }

    [Fact]
    public async Task Emits_an_id_tagged_group_per_door_and_stair()
    {
        VectorMap map = await Sample();
        string svg = VectorMapSvgRenderer.Render(map, VectorMapRenderOptions.Default);

        Assert.Equal(map.Doors.Count, Count(svg, "data-object-kind=\"door\""));
        Assert.Equal(map.Stairs.Count, Count(svg, "data-object-kind=\"stairs\""));
    }

    [Fact]
    public async Task Grid_toggle_removes_the_grid_lines()
    {
        VectorMap map = await Sample();
        string withGrid = VectorMapSvgRenderer.Render(map, new VectorMapRenderOptions { ShowGrid = true });
        string without = VectorMapSvgRenderer.Render(map, new VectorMapRenderOptions { ShowGrid = false });

        Assert.Contains("<line ", withGrid);
        Assert.DoesNotContain("<line ", without);
    }

    [Fact]
    public async Task Object_toggle_removes_the_door_and_stair_pass()
    {
        VectorMap map = await Sample();
        string without = VectorMapSvgRenderer.Render(map, new VectorMapRenderOptions { ShowObjects = false });

        Assert.DoesNotContain("data-object-kind", without);
    }

    [Fact]
    public async Task Renders_decoration_sprites_once_and_places_each()
    {
        VectorMap map = await Sample();
        string svg = VectorMapSvgRenderer.Render(map, VectorMapRenderOptions.Default);

        // Each distinct sprite is embedded once in <defs>; every placement is a lightweight <use>.
        Assert.Equal(map.Sprites.Count, Count(svg, "<image id=\"sp-"));
        Assert.Equal(map.Decorations.Count, Count(svg, "<use href=\"#sp-"));
        // Sprites are top-left anchored (Dungeon Scrawl's placement origin), not centred.
        Assert.Equal(map.Sprites.Count, Count(svg, "x=\"0\" y=\"0\""));
    }

    [Fact]
    public async Task Object_toggle_removes_decorations_and_their_sprite_defs()
    {
        string svg = VectorMapSvgRenderer.Render(await Sample(), new VectorMapRenderOptions { ShowObjects = false });

        Assert.DoesNotContain("<use href=\"#sp-", svg);
        Assert.DoesNotContain("<image id=\"sp-", svg);
    }

    [Fact]
    public void Decoration_use_carries_the_placement_matrix_and_alpha()
    {
        var map = new VectorMap
        {
            Bounds = new MapBounds(0, 0, 100, 100),
            Sprites = new[] { new SpriteImage { AssetId = "a", DataUri = "data:image/png;base64,iVBORx", Width = 36, Height = 48 } },
            Decorations = new[]
            {
                new MapDecoration { AssetId = "a", Transform = new AffineTransform(2, 0, 0, 3, 100, 200), Alpha = 0.5 },
                new MapDecoration { AssetId = "a", Transform = AffineTransform.Identity, Alpha = 1 },
            },
        };

        string svg = VectorMapSvgRenderer.Render(map, VectorMapRenderOptions.Default);

        // Sprite defined once, top-left anchored at world size.
        Assert.Contains("<image id=\"sp-a\" x=\"0\" y=\"0\" width=\"36\" height=\"48\"", svg);
        // Matrix terms emitted in a,b,c,d,e,f order; alpha < 1 adds opacity, alpha == 1 does not.
        Assert.Contains("transform=\"matrix(2 0 0 3 100 200)\" opacity=\"0.5\"", svg);
        Assert.Contains("transform=\"matrix(1 0 0 1 0 0)\"/>", svg);
    }

    [Fact]
    public void Decoration_sprite_ids_and_data_uris_are_attribute_escaped()
    {
        var map = new VectorMap
        {
            Bounds = new MapBounds(0, 0, 10, 10),
            Sprites = new[] { new SpriteImage { AssetId = "x\"><b", DataUri = "data:image/png;base64,\"><script>", Width = 10, Height = 10 } },
            Decorations = new[] { new MapDecoration { AssetId = "x\"><b", Transform = AffineTransform.Identity } },
        };

        string svg = VectorMapSvgRenderer.Render(map, VectorMapRenderOptions.Default);

        Assert.DoesNotContain("<script>", svg);
        Assert.Contains("id=\"sp-x&quot;&gt;&lt;b\"", svg);
        Assert.Contains("href=\"data:image/png;base64,&quot;&gt;&lt;script&gt;\"", svg);
    }

    [Fact]
    public async Task Wall_shading_is_drawn_by_default_and_removable()
    {
        VectorMap map = await Sample();

        string on = VectorMapSvgRenderer.Render(map, VectorMapRenderOptions.Default);
        Assert.Contains("id=\"dt-hatch\"", on);
        Assert.Contains("mask=\"url(#dt-band)\"", on);

        string off = VectorMapSvgRenderer.Render(map, new VectorMapRenderOptions { ShowWallShading = false });
        Assert.DoesNotContain("dt-hatch", off);
        Assert.DoesNotContain("dt-band", off);
    }

    [Fact]
    public async Task ViewBox_override_restricts_the_svg_to_the_given_region()
    {
        VectorMap map = await Sample();

        // Default: viewBox is the padded full-map bounds.
        string full = VectorMapSvgRenderer.Render(map, VectorMapRenderOptions.Default);
        Assert.Contains(PaddedViewBox(map.Bounds), full);

        // Override: the player region drives the viewBox verbatim.
        string region = VectorMapSvgRenderer.Render(
            map, new VectorMapRenderOptions { ViewBox = new MapBounds(2000, 3000, 4400, 4350) });
        Assert.Contains("viewBox=\"2000 3000 2400 1350\"", region);
    }

    [Fact]
    public void Empty_map_renders_a_valid_empty_svg_without_throwing()
    {
        string svg = VectorMapSvgRenderer.Render(new VectorMap(), VectorMapRenderOptions.Default);

        Assert.StartsWith("<svg", svg);
        Assert.EndsWith("</svg>", svg);
        Assert.DoesNotContain("data-object-kind", svg);
    }

    [Fact]
    public async Task Layers_are_emitted_floor_then_walls_then_objects()
    {
        string svg = VectorMapSvgRenderer.Render(await Sample(), VectorMapRenderOptions.Default);

        int floor = svg.IndexOf("fill=\"#F0ECE0\"", StringComparison.Ordinal);
        int walls = svg.IndexOf("stroke=\"#000000\" stroke-width=\"5\"", StringComparison.Ordinal);
        int objects = svg.IndexOf("data-object-kind", StringComparison.Ordinal);

        Assert.True(floor >= 0 && walls >= 0 && objects >= 0);
        Assert.True(floor < walls, "floor fill must be painted before the wall stroke");
        Assert.True(walls < objects, "walls must be painted before the object pass");
    }

    [Fact]
    public async Task Inner_shadow_is_off_by_default_and_can_be_enabled()
    {
        VectorMap map = await Sample();

        // Off by default (it read as an odd double line); the offset transform is unique to it.
        string off = VectorMapSvgRenderer.Render(map, VectorMapRenderOptions.Default);
        Assert.DoesNotContain("translate(12,9)", off);

        string on = VectorMapSvgRenderer.Render(map, new VectorMapRenderOptions { ShowShadow = true });
        Assert.Contains("translate(12,9)", on);
    }

    [Fact]
    public void Object_ids_are_attribute_escaped_against_markup_injection()
    {
        // The SVG is injected raw via (MarkupString); a hostile id must not break out of the
        // attribute or inject markup.
        var map = new VectorMap { Doors = new[] { new DoorFeature { Id = "x\"><script>&" } } };

        string svg = VectorMapSvgRenderer.Render(map, VectorMapRenderOptions.Default);

        Assert.Contains("data-object-id=\"x&quot;&gt;&lt;script&gt;&amp;\"", svg);
        Assert.DoesNotContain("<script>", svg);
    }

    [Fact]
    public void Floor_polygon_holes_are_emitted_as_separate_subpaths_with_evenodd()
    {
        var outer = new PolygonRing
        {
            Vertices = new[]
            {
                new MapPoint(0, 0), new MapPoint(100, 0), new MapPoint(100, 100),
                new MapPoint(0, 100), new MapPoint(0, 0),
            },
        };
        var hole = new PolygonRing
        {
            Vertices = new[]
            {
                new MapPoint(25, 25), new MapPoint(75, 25), new MapPoint(75, 75),
                new MapPoint(25, 75), new MapPoint(25, 25),
            },
        };
        var map = new VectorMap
        {
            Bounds = new MapBounds(0, 0, 100, 100),
            Layers = new[]
            {
                new MapLayer
                {
                    Kind = LayerKind.Floor,
                    Fill = new FillStyle { Visible = true, Colour = new Rgba(0x12, 0x34, 0x56, 255) },
                    Geometry = new VectorGeometry
                    {
                        Polygons = new[] { new MapPolygon { Rings = new[] { outer, hole } } },
                    },
                },
            },
        };

        string svg = VectorMapSvgRenderer.Render(map, VectorMapRenderOptions.Default);

        Match fill = Regex.Match(svg, "d=\"([^\"]*)\" fill=\"#123456\" fill-rule=\"evenodd\"");
        Assert.True(fill.Success, "floor fill path with evenodd not found");
        // Outer ring + hole ring => two closed subpaths.
        Assert.Equal(2, Regex.Count(fill.Groups[1].Value, "M"));
        Assert.Equal(2, Regex.Count(fill.Groups[1].Value, "Z"));
    }
}
