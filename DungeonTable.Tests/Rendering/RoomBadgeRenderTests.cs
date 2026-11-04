using System.Collections.Generic;
using DungeonTable.Core.Maps;
using DungeonTable.Web.Rendering;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// The rooms' labels and badges on the DM's map: the badges sit under the label they belong to, a
/// cleared room is greyed, and labels carry what the canvas's CSS needs to keep them one size on screen.
/// </summary>
public sealed class RoomBadgeRenderTests
{
    private static readonly Region Den = Square("r-den", "Goblin Den", 0, 0);
    private static readonly Region Nave = Square("r-nave", "Nave", 100, 0);

    [Fact]
    public void The_badges_sit_under_their_room_s_label_and_another_room_keeps_a_plain_label()
    {
        var badges = new MapBadges
        {
            ByRegion = new Dictionary<string, RoomBadges>(StringComparer.Ordinal)
            {
                ["r-den"] = new RoomBadges { Creatures = "1d4+3", Loot = true, Secret = true },
            },
        };

        string svg = Render(badges, string.Empty);

        // Two text elements, the name half a line up and the badges half a line down, so the canvas can
        // hide a name that does not fit its room and keep the badges.
        Assert.Contains("<text class=\"dt-label\" x=\"20\" y=\"20\" dy=\"-0.6em\" ", svg, StringComparison.Ordinal);
        Assert.Contains("pointer-events=\"none\">Goblin Den</text><text class=\"dt-label dt-badges\" x=\"20\" y=\"20\" dy=\"0.6em\" ", svg, StringComparison.Ordinal);
        Assert.Contains(
            "<tspan class=\"dt-badge\" fill=\"#b3261e\">☠\uFE0E 1d4+3</tspan>"
            + "<tspan class=\"dt-badge\" fill=\"#9a6b00\" dx=\"0.35em\">◆</tspan>"
            + "<tspan class=\"dt-badge\" fill=\"#7b3fa0\" dx=\"0.35em\">S</tspan></text>",
            svg,
            StringComparison.Ordinal);

        // The nave has no badges: one line, on the middle.
        Assert.Contains("<text class=\"dt-label\" x=\"120\" y=\"20\" font-size=", svg, StringComparison.Ordinal);
        Assert.Contains("pointer-events=\"none\">Nave</text>", svg, StringComparison.Ordinal);
        Assert.Equal(1, svg.Split("class=\"dt-label dt-badges\"").Length - 1);
    }

    [Fact]
    public void A_cleared_room_is_greyed_unless_it_is_the_one_selected()
    {
        var badges = new MapBadges
        {
            ByRegion = new Dictionary<string, RoomBadges>(StringComparer.Ordinal) { ["r-den"] = new RoomBadges { Cleared = true } },
            ClearedRegionIds = new[] { "r-den", "r-nave" },
        };

        string svg = Render(badges, "r-nave");

        Assert.Contains("<g data-region-id=\"r-den\" class=\"dt-cleared\"><polygon points=\"0,0 40,0 40,40 0,40\" fill=\"rgba(110,110,110,0.30)\" stroke=\"#8a8a8a\"", svg, StringComparison.Ordinal);
        Assert.Contains("<g data-region-id=\"r-nave\"><polygon", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"#2e7d32\">✔\uFE0E</tspan>", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Labels_carry_what_keeps_them_one_size_on_screen()
    {
        string svg = Render(MapBadges.None, string.Empty);

        // The overlay says how big a label was in map units, per screen pixel of the 14 px label, for the
        // first paint; the label's class is what the canvas's CSS sizes from --dt-u after that.
        Assert.Contains("<g class=\"editor-overlay\" style=\"--dt-u0:0.607\">", svg, StringComparison.Ordinal);
        Assert.Contains("<text class=\"dt-label\" x=\"20\" y=\"20\" font-size=\"8.5\"", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-badge", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("dt-cleared", svg, StringComparison.Ordinal);
    }

    private static string Render(MapBadges badges, string selected)
    {
        var options = new VectorMapRenderOptions
        {
            Overlay = new EditorOverlay
            {
                Regions = new[] { Den, Nave },
                Badges = badges,
                SelectedRegionId = selected,
                UnitSize = 10,
            },
        };

        return VectorMapSvgRenderer.RenderOverlayLayer(options);
    }

    private static Region Square(string id, string label, double x, double y) =>
        new Region
        {
            RegionId = id,
            Label = label,
            GraphNodeId = "area-" + id,
            Polygon = new[] { new MapPoint(x, y), new MapPoint(x + 40, y), new MapPoint(x + 40, y + 40), new MapPoint(x, y + 40) },
        };
}
