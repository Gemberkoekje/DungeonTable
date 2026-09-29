using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// Guards the concealed-object contract: a decoration or staircase marked concealed in the Room
/// Editor is drawn GHOSTED (but still selectable) on the DM's own map, and is absent entirely from
/// the player projector, until the DM reveals it for the session — at which point both views show
/// it normally. Concealment rides the same reveal set a discovered secret door uses, so one DM
/// click covers both.
/// </summary>
public sealed class ConcealedObjectRenderTests
{
    private const string StatueId = "dec-1";
    private const string StairId = "stair-1";
    private const string AssetId = "asset-1";

    private static VectorMap MapWithStatueAndStairs() =>
        new VectorMap
        {
            Bounds = new MapBounds(0, 0, 100, 100),
            Grid = new Grid { CellSizePx = 10 },
            Layers = new[]
            {
                new MapLayer
                {
                    Kind = LayerKind.Floor,
                    Fill = new FillStyle { Visible = true, Colour = new Rgba(244, 241, 234, 255) },
                    Geometry = Rect(0, 0, 100, 100),
                },
            },
            Sprites = new[]
            {
                new SpriteImage { AssetId = AssetId, DataUri = "data:image/png;base64,iVBOR", Width = 10, Height = 10 },
            },
            Decorations = new[]
            {
                new MapDecoration
                {
                    Id = StatueId,
                    Name = "Statue1x1",
                    AssetId = AssetId,
                    Transform = new AffineTransform(1, 0, 0, 1, 20, 20),
                    Bounds = new MapBounds(20, 20, 30, 30),
                },
            },
            Stairs = new[]
            {
                new StairFeature
                {
                    Id = StairId,
                    Bounds = new MapBounds(60, 60, 80, 80),
                    Geometry = new VectorGeometry
                    {
                        Polylines = new[]
                        {
                            new Polyline { Points = new[] { new MapPoint(60, 60), new MapPoint(80, 60) } },
                        },
                    },
                },
            },
        };

    private static VectorGeometry Rect(double x0, double y0, double x1, double y1) =>
        new VectorGeometry
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
                                new MapPoint(x0, y0), new MapPoint(x1, y0),
                                new MapPoint(x1, y1), new MapPoint(x0, y1), new MapPoint(x0, y0),
                            },
                        },
                    },
                },
            },
        };

    private static string DmRender(ConcealState conceal) =>
        VectorMapSvgRenderer.Render(MapWithStatueAndStairs(), new VectorMapRenderOptions { Conceal = conceal });

    private static string PlayerRender(ConcealState conceal) =>
        VectorMapSvgRenderer.RenderRevealed(
            MapWithStatueAndStairs(),
            default,
            new[] { new[] { new MapPoint(0, 0), new MapPoint(100, 0), new MapPoint(100, 100), new MapPoint(0, 100) } },
            Array.Empty<MapBounds>(),
            Array.Empty<FeatureMarker>(),
            DoorRenderState.None,
            conceal);

    [Fact]
    public void An_unconcealed_decoration_draws_solid_and_selectable_on_the_dm_view()
    {
        // The baseline the concealed cases are measured against: no annotation, no ghosting.
        string svg = DmRender(ConcealState.None);

        Assert.Contains("data-object-kind=\"decoration\" data-object-id=\"dec-1\"", svg, StringComparison.Ordinal);
        Assert.Contains("href=\"#sp-asset-1\"", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("opacity=\"0.3\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void A_concealed_decoration_is_ghosted_but_still_selectable_on_the_dm_view()
    {
        string svg = DmRender(new ConcealState { ConcealedIds = new[] { StatueId } });

        // Ghosted, not gone: the DM keeps seeing it (and can click it to reveal it).
        Assert.Contains("opacity=\"0.3\"", svg, StringComparison.Ordinal);
        Assert.Contains("data-object-id=\"dec-1\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Revealing_a_concealed_decoration_makes_it_solid_again_on_the_dm_view()
    {
        var conceal = new ConcealState { ConcealedIds = new[] { StatueId }, RevealedIds = new[] { StatueId } };

        string svg = DmRender(conceal);

        Assert.Contains("href=\"#sp-asset-1\"", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("opacity=\"0.3\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void A_concealed_decoration_never_reaches_the_player_projector()
    {
        // The whole room is revealed, so only concealment can be keeping the statue off the screen.
        string visible = PlayerRender(ConcealState.None);
        Assert.Contains("href=\"#sp-asset-1\"", visible, StringComparison.Ordinal);

        string concealed = PlayerRender(new ConcealState { ConcealedIds = new[] { StatueId } });
        Assert.DoesNotContain("href=\"#sp-asset-1\"", concealed, StringComparison.Ordinal);
        Assert.DoesNotContain("dec-1", concealed, StringComparison.Ordinal);
    }

    [Fact]
    public void A_revealed_decoration_reaches_the_player_projector()
    {
        var conceal = new ConcealState { ConcealedIds = new[] { StatueId }, RevealedIds = new[] { StatueId } };

        string svg = PlayerRender(conceal);

        Assert.Contains("href=\"#sp-asset-1\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void A_concealed_staircase_is_ghosted_on_the_dm_view_and_absent_from_the_player_view()
    {
        var conceal = new ConcealState { ConcealedIds = new[] { StairId } };

        // The stair polyline is stroked at reduced opacity for the DM...
        string dm = DmRender(conceal);
        Assert.Contains("data-object-id=\"stair-1\"", dm, StringComparison.Ordinal);
        Assert.Contains("stroke-opacity=\"0.3\"", dm, StringComparison.Ordinal);

        // ...and its geometry is not emitted at all for the players, while an unconcealed one is.
        string player = PlayerRender(conceal);
        string baseline = PlayerRender(ConcealState.None);
        Assert.Contains("M60 60L80 60", baseline, StringComparison.Ordinal);
        Assert.DoesNotContain("M60 60L80 60", player, StringComparison.Ordinal);
    }

    [Fact]
    public void Concealing_one_object_leaves_the_others_alone()
    {
        // A shared reveal set means a stray id could hide the wrong thing; the stairs must be
        // untouched when only the statue is concealed.
        string dm = DmRender(new ConcealState { ConcealedIds = new[] { StatueId } });

        Assert.DoesNotContain("stroke-opacity=\"0.3\"", dm, StringComparison.Ordinal);
        Assert.Contains("M60 60L80 60", PlayerRender(new ConcealState { ConcealedIds = new[] { StatueId } }), StringComparison.Ordinal);
    }

    [Fact]
    public void The_editor_outlines_a_concealed_object_like_a_secret_one()
    {
        var overlay = new EditorOverlay
        {
            Objects = new[]
            {
                new ObjectHighlight
                {
                    Id = StatueId,
                    Kind = MapObjectKind.Decoration,
                    Bounds = new MapBounds(20, 20, 30, 30),
                    IsConcealed = true,
                },
            },
            UnitSize = 10,
        };

        string svg = VectorMapSvgRenderer.Render(
            MapWithStatueAndStairs(), new VectorMapRenderOptions { Overlay = overlay });

        Assert.Contains("stroke=\"#8a2be2\"", svg, StringComparison.Ordinal); // the hidden-from-players outline
        Assert.Contains("stroke-dasharray=", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void An_editor_hit_target_for_a_decoration_carries_the_decoration_hook()
    {
        // The input layer reports data-object-kind back as the click's hit kind, so the editor and
        // the DM's Conceal mode both depend on this string being "decoration" and not "door".
        var overlay = new EditorOverlay
        {
            HitTargets = new[]
            {
                new ObjectHighlight { Id = StatueId, Kind = MapObjectKind.Decoration, Bounds = new MapBounds(20, 20, 30, 30) },
            },
            UnitSize = 10,
        };

        string svg = VectorMapSvgRenderer.Render(
            MapWithStatueAndStairs(), new VectorMapRenderOptions { Overlay = overlay });

        Assert.Contains("data-object-kind=\"decoration\" data-object-id=\"dec-1\"", svg, StringComparison.Ordinal);
    }
}
