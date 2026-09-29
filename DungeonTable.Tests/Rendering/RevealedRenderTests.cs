using System.Collections.Generic;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// Regression guards for the player reveal render (<see cref="VectorMapSvgRenderer.RenderRevealed"/>).
/// The tile model: revealed tiles draw the real floor clipped to <c>dt-drawn</c>; the wall band is the
/// drawn floor dilated (<c>dt-band</c>), so a hidden door reads as wall and nothing beyond it is drawn;
/// a hidden door is skipped (no door slab) while a normal door draws as a door; and an open doorway (but
/// never a hidden door) yields a dimmed peek.
/// </summary>
public sealed class RevealedRenderTests
{
    private const string DoorId = "door-1";

    private static readonly IReadOnlyList<MapPoint> Room = new[]
    {
        new MapPoint(0, 0), new MapPoint(40, 0), new MapPoint(40, 40), new MapPoint(0, 40),
    };

    // A 4x4 room (0..40) walled all round, with a one-cell gap + door in the bottom wall at x 10..20,
    // and a one-cell corridor stub dropping below that door (revealed only via the door).
    private static VectorMap RoomWithDoorAndCorridor() =>
        new VectorMap
        {
            Grid = new Grid { CellSizePx = 10 },
            Bounds = new MapBounds(0, 0, 40, 70),
            Layers = new[]
            {
                new MapLayer
                {
                    Kind = LayerKind.Floor,
                    Geometry = new VectorGeometry { Polygons = new[] { Ring(0, 0, 40, 40), Ring(10, 40, 20, 70) } },
                },
                new MapLayer
                {
                    Kind = LayerKind.Walls,
                    Geometry = new VectorGeometry
                    {
                        Polylines = new[]
                        {
                            Line(0, 0, 40, 0),
                            Line(0, 0, 0, 40),
                            Line(40, 0, 40, 40),
                            Line(0, 40, 10, 40),
                            Line(20, 40, 40, 40),
                            Line(10, 40, 10, 70),
                            Line(20, 40, 20, 70),
                        },
                    },
                },
            },
            Doors = new[]
            {
                new DoorFeature
                {
                    Id = DoorId,
                    Orientation = DoorOrientation.Horizontal,
                    Bounds = new MapBounds(10, 38, 20, 42),
                    Geometry = new VectorGeometry { Polygons = new[] { Ring(10, 38, 20, 42) } },
                },
            },
        };

    private static MapPolygon Ring(double x0, double y0, double x1, double y1) =>
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

    private static Polyline Line(double x0, double y0, double x1, double y1) =>
        new Polyline { Points = new[] { new MapPoint(x0, y0), new MapPoint(x1, y1) } };

    private static string Render(IReadOnlyList<IReadOnlyList<MapPoint>> regions, IReadOnlyCollection<string> secrets) =>
        VectorMapSvgRenderer.RenderRevealed(
            RoomWithDoorAndCorridor(), default, regions, Array.Empty<MapBounds>(), Array.Empty<FeatureMarker>(),
            new DoorRenderState { SecretDoorIds = secrets }, ConcealState.None);

    [Fact]
    public void A_lit_room_draws_real_floor_and_a_dilated_wall_band()
    {
        string svg = Render(new[] { Room }, Array.Empty<string>());

        Assert.Contains("fill=\"#05060a\"", svg, StringComparison.Ordinal);                    // black background
        Assert.Contains("<clipPath id=\"dt-drawn\"", svg, StringComparison.Ordinal);           // clip to drawn tiles
        Assert.Contains("<mask id=\"dt-b-r\"", svg, StringComparison.Ordinal);                 // revealed layer's wall band
        Assert.Contains("feMorphology operator=\"dilate\"", svg, StringComparison.Ordinal);    // the dilation itself
        Assert.Contains("<g clip-path=\"url(#dt-drawn)\">", svg, StringComparison.Ordinal);    // objects on drawn tiles
        Assert.Contains("fill=\"url(#dt-hatch)\"", svg, StringComparison.Ordinal);             // book hatch on the band
    }

    [Fact]
    public void A_hidden_door_is_not_drawn_as_a_door()
    {
        // Marked hidden, the door is skipped entirely — no door slab; the wall band covers it as wall.
        string svg = Render(new[] { Room }, new[] { DoorId });

        Assert.DoesNotContain("fill=\"#FFFFFF\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void A_normal_door_is_drawn_as_a_door()
    {
        // Guards the previous test: unmarked, the same door draws as a door (its white slab), so it is
        // the hidden handling that turns it into wall.
        string svg = Render(new[] { Room }, Array.Empty<string>());

        Assert.Contains("fill=\"#FFFFFF\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void A_discovered_secret_door_renders_as_a_door_but_an_undiscovered_one_stays_wall()
    {
        // The invariant: a secret door is wall until discovered. Undiscovered -> no door slab...
        string hidden = Render(new[] { Room }, new[] { DoorId });
        Assert.DoesNotContain("fill=\"#FFFFFF\"", hidden, StringComparison.Ordinal);

        // ...discovered -> it draws as a real door (white slab) for the players.
        string discovered = VectorMapSvgRenderer.RenderRevealed(
            RoomWithDoorAndCorridor(), default, new[] { Room }, Array.Empty<MapBounds>(), Array.Empty<FeatureMarker>(),
            new DoorRenderState { SecretDoorIds = new[] { DoorId }, DiscoveredDoorIds = new[] { DoorId } },
            ConcealState.None);
        Assert.Contains("fill=\"#FFFFFF\"", discovered, StringComparison.Ordinal);
    }

    [Fact]
    public void An_open_doorway_peeks_but_a_hidden_door_shows_nothing_behind_it()
    {
        // The corridor stub below the open door becomes a dimmed peek layer (a group with opacity < 1)...
        string open = Render(new[] { Room }, Array.Empty<string>());
        Assert.Contains("<g opacity=", open, StringComparison.Ordinal);

        // ...but a hidden door blocks the reach, so there is no peek layer behind it.
        string secret = Render(new[] { Room }, new[] { DoorId });
        Assert.DoesNotContain("<g opacity=", secret, StringComparison.Ordinal);
    }

    [Fact]
    public void Nothing_revealed_shows_only_black_with_no_content()
    {
        string svg = Render(Array.Empty<IReadOnlyList<MapPoint>>(), Array.Empty<string>());

        Assert.Contains("fill=\"#05060a\"", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<g clip-path=\"url(#dt-drawn)\">", svg, StringComparison.Ordinal); // no floor drawn
        Assert.DoesNotContain("fill=\"url(#dt-hatch)\"", svg, StringComparison.Ordinal);          // no wall band drawn
    }

    [Fact]
    public void A_revealed_marker_is_drawn()
    {
        string svg = VectorMapSvgRenderer.RenderRevealed(
            RoomWithDoorAndCorridor(), default, new[] { Room }, Array.Empty<MapBounds>(),
            new[] { new FeatureMarker { FeatureId = "trap-1", Kind = FeatureKind.Trap, Position = new MapPoint(20, 20) } },
            DoorRenderState.None, ConcealState.None);

        Assert.Contains("data-feature-id=\"trap-1\"", svg, StringComparison.Ordinal);
    }
}
