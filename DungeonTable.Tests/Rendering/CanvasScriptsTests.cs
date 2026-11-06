using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Core.Session;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// The map canvas tells its scripts after a render only what changed since it last told them
/// (<see cref="CanvasScripts"/>): on a phone's hotspot every call is a round trip, and the DM's map
/// renders several times a second while the party token moves.
/// </summary>
public sealed class CanvasScriptsTests
{
    private const string ViewBox = "-24 -24 448 448";

    private static readonly Grid Grid = new Grid { CellSizePx = 10, OriginX = 0, OriginY = 0 };

    private static readonly MapBounds Frame = new MapBounds(0, 0, 160, 90);

    private static readonly PartyToken Party = new PartyToken { IsPlaced = true, X = 25, Y = 25, VisionFeet = 30 };

    [Fact]
    public void The_first_time_everything_is_told()
    {
        var told = new CanvasScripts();

        Assert.True(told.PanZoomChanged(ViewBox));
        Assert.True(told.PlayerBoxChanged(Frame, 16.0 / 9.0, 0, held: false));
        Assert.True(told.PartyChanged(Party, 60, Grid, draggable: true));
        Assert.True(told.EditorChanged("select", 10, Grid));
    }

    [Fact]
    public void The_same_values_again_tell_nothing()
    {
        CanvasScripts told = ToldOnce();

        Assert.False(told.PanZoomChanged(ViewBox));
        Assert.False(told.PlayerBoxChanged(Frame, 16.0 / 9.0, 0, held: false));
        Assert.False(told.PartyChanged(Party, 60, Grid, draggable: true));
        Assert.False(told.EditorChanged("select", 10, Grid));
    }

    [Fact]
    public void A_moved_token_is_told_and_nothing_else_changes()
    {
        CanvasScripts told = ToldOnce();

        Assert.True(told.PartyChanged(new PartyToken { IsPlaced = true, X = 35, Y = 25, VisionFeet = 30 }, 60, Grid, draggable: true));
        Assert.False(told.PlayerBoxChanged(Frame, 16.0 / 9.0, 0, held: false));
    }

    [Fact]
    public void A_moved_frame_a_turn_or_a_held_map_is_told()
    {
        CanvasScripts told = ToldOnce();

        Assert.True(told.PlayerBoxChanged(new MapBounds(10, 0, 170, 90), 16.0 / 9.0, 0, held: false));
        Assert.True(told.PlayerBoxChanged(new MapBounds(10, 0, 170, 90), 16.0 / 9.0, 1, held: false));
        Assert.True(told.PlayerBoxChanged(new MapBounds(10, 0, 170, 90), 16.0 / 9.0, 1, held: true));
    }

    [Fact]
    public void A_token_the_script_moved_itself_is_told_again_where_the_server_left_it()
    {
        // The DM let go in the square the token started from: the server has it where it was, the script
        // where the pointer was.
        CanvasScripts told = ToldOnce();

        told.ForgetParty();

        Assert.True(told.PartyChanged(Party, 60, Grid, draggable: true));
    }

    [Fact]
    public void A_box_the_script_moved_itself_is_told_again_where_the_server_left_it()
    {
        // Dragged off the map, the box is put back by the server where it was before the drag.
        CanvasScripts told = ToldOnce();

        told.ForgetPlayerBox();

        Assert.True(told.PlayerBoxChanged(Frame, 16.0 / 9.0, 0, held: false));
    }

    [Fact]
    public void A_box_set_up_at_its_values_is_not_told_them_again()
    {
        var told = new CanvasScripts();

        told.PlayerBoxSetUp(Frame, 16.0 / 9.0, 0, held: false);

        Assert.False(told.PlayerBoxChanged(Frame, 16.0 / 9.0, 0, held: false));
    }

    [Fact]
    public void A_new_map_applies_the_pan_and_zoom_again()
    {
        CanvasScripts told = ToldOnce();

        Assert.True(told.PanZoomChanged("-24 -24 900 700"));
    }

    [Fact]
    public void The_polygon_tool_is_told_after_every_render()
    {
        // Its rubber band runs from the outline's last corner, which a render moves.
        var told = new CanvasScripts();
        told.EditorChanged("draw-poly", 10, Grid);

        Assert.True(told.EditorChanged("draw-poly", 10, Grid));
    }

    private static CanvasScripts ToldOnce()
    {
        var told = new CanvasScripts();
        told.PanZoomChanged(ViewBox);
        told.PlayerBoxChanged(Frame, 16.0 / 9.0, 0, held: false);
        told.PartyChanged(Party, 60, Grid, draggable: true);
        told.EditorChanged("select", 10, Grid);
        return told;
    }
}
