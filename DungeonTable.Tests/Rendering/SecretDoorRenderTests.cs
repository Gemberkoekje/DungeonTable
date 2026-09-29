using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// Verifies that a door marked secret renders as a wall — with an "S" and an object hook on the DM
/// view, and as an anonymous wall (no marker, no <c>data-object-*</c> hook) on the player view — so
/// the player cannot tell a secret door from the surrounding wall.
/// </summary>
public sealed class SecretDoorRenderTests
{
    private const string DoorId = "door-1";

    private static VectorMap MapWithOneDoor() =>
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
    public void Non_secret_door_renders_as_a_door_glyph()
    {
        string svg = VectorMapSvgRenderer.Render(MapWithOneDoor(), VectorMapRenderOptions.Default);

        Assert.Contains("data-object-id=\"door-1\"", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"#FFFFFF\"", svg, StringComparison.Ordinal); // white door slab
        Assert.DoesNotContain(">S</text>", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Secret_door_on_the_dm_view_is_a_wall_with_an_s_and_stays_selectable()
    {
        var options = new VectorMapRenderOptions { Doors = new DoorRenderState { SecretDoorIds = new[] { DoorId } }, MarkSecretDoors = true };

        string svg = VectorMapSvgRenderer.Render(MapWithOneDoor(), options);

        Assert.Contains("data-object-id=\"door-1\"", svg, StringComparison.Ordinal); // selectable
        Assert.Contains(">S</text>", svg, StringComparison.Ordinal);                 // marked
        Assert.DoesNotContain("fill=\"#FFFFFF\"", svg, StringComparison.Ordinal);    // not a door slab
    }

    [Fact]
    public void Secret_door_in_a_vertical_wall_keeps_the_s_upright()
    {
        // A door set in a vertical (N-S) wall — walked through going east/west — is upright.
        var map = new VectorMap
        {
            Bounds = new MapBounds(0, 0, 100, 100),
            Grid = new Grid { CellSizePx = 10 },
            Doors = new[]
            {
                new DoorFeature
                {
                    Id = DoorId,
                    Orientation = DoorOrientation.Vertical,
                    Bounds = new MapBounds(48, 40, 52, 60), // a tall (N-S) door leaf
                },
            },
        };
        var options = new VectorMapRenderOptions { Doors = new DoorRenderState { SecretDoorIds = new[] { DoorId } }, MarkSecretDoors = true };

        string svg = VectorMapSvgRenderer.Render(map, options);

        Assert.Contains("fill=\"#1b1b1b\"", svg, StringComparison.Ordinal); // book-style black "S"
        Assert.DoesNotContain("rotate(90", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Secret_door_in_a_horizontal_wall_lays_the_s_sideways()
    {
        var options = new VectorMapRenderOptions { Doors = new DoorRenderState { SecretDoorIds = new[] { DoorId } }, MarkSecretDoors = true };

        // The sample door is horizontal (20 wide x 4 tall) — an E-W wall walked through north/south —
        // so its "S" is turned sideways to lie along the wall.
        string svg = VectorMapSvgRenderer.Render(MapWithOneDoor(), options);

        Assert.Contains(">S</text>", svg, StringComparison.Ordinal);
        Assert.Contains("rotate(90", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Secret_door_on_the_player_view_is_an_anonymous_wall()
    {
        var options = new VectorMapRenderOptions { Doors = new DoorRenderState { SecretDoorIds = new[] { DoorId } }, MarkSecretDoors = false };

        string svg = VectorMapSvgRenderer.Render(MapWithOneDoor(), options);

        // No "S", no door slab, and — crucially — no object hook that would betray the door.
        Assert.DoesNotContain(">S</text>", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("data-object-id=\"door-1\"", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("fill=\"#FFFFFF\"", svg, StringComparison.Ordinal);

        // The heal is a wall-style <path> stroke, not a distinctive filled <rect>: a lone
        // wall-coloured rect in the player SVG would let anyone reading the source enumerate every
        // secret door's position. This sample map draws no floor/grid, so there must be no rect at all.
        Assert.DoesNotContain("<rect", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"none\" stroke=", svg, StringComparison.Ordinal);
    }
}
