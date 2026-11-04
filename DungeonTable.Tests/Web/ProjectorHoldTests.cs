using DungeonTable.Core.Maps.Vector;
using DungeonTable.Core.Session;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The projector stays on the map the players are on when the DM moves to another one, with what was
/// revealed there and its framing, until the DM shows them the new map
/// (<see cref="SessionState.ShowCurrentMapOnProjector"/>).
/// </summary>
public sealed class ProjectorHoldTests
{
    private static readonly MapBounds UpperFraming = new MapBounds(10, 20, 410, 245);
    private static readonly MapBounds LowerFraming = new MapBounds(500, 500, 900, 725);
    private static readonly string[] RoomOne = { "room-1" };
    private static readonly string[] RoomNine = { "room-9" };
    private static readonly string[] TrapOne = { "trap-1" };
    private static readonly string[] DoorOne = { "door-1" };
    private static readonly (int Col, int Row)[] CellFourNine = { (4, 9) };

    [Fact]
    public void The_first_map_the_table_is_on_goes_straight_to_the_projector()
    {
        var state = new SessionState();

        state.SetCurrentMap("level-1");

        Assert.Equal("level-1", state.ProjectorMapId);
        Assert.False(state.ProjectorOnAnotherMap);
        Assert.True(state.Projector().IsCurrentMap);
    }

    [Fact]
    public void Moving_to_another_map_leaves_the_projector_on_the_one_the_players_see()
    {
        SessionState state = DownAFloor();

        Assert.Equal("level-2", state.CurrentMapId);
        Assert.Equal("level-1", state.ProjectorMapId);
        Assert.True(state.ProjectorOnAnotherMap);

        ProjectorView projector = state.Projector();
        Assert.Equal("level-1", projector.MapId);
        Assert.False(projector.IsCurrentMap);
        Assert.Equal(RoomOne, projector.RevealedRegionIds);
        Assert.Equal(TrapOne, projector.RevealedFeatureIds);
        Assert.Equal(DoorOne, projector.OpenDoorIds);
        Assert.Equal(CellFourNine, projector.FogCells);
        Assert.Equal(UpperFraming, projector.Viewport);
    }

    [Fact]
    public void What_the_dm_does_on_the_new_map_stays_off_the_projector_until_it_is_shown()
    {
        // The DM sets the next floor up (reveals its landing, frames it) while the players still look at
        // the floor they are on.
        SessionState state = DownAFloor();

        Assert.Equal(RoomNine, state.RevealedRegionIds());
        Assert.Equal(LowerFraming, state.PlayerViewport);

        ProjectorView projector = state.Projector();
        Assert.DoesNotContain("room-9", projector.RevealedRegionIds);
        Assert.DoesNotContain((7, 7), projector.FogCells);
        Assert.Equal(UpperFraming, projector.Viewport);
    }

    [Fact]
    public void Showing_the_map_puts_it_on_the_projector_with_its_reveals_and_framing()
    {
        SessionState state = DownAFloor();
        int changes = 0;
        state.Changed += () => changes++;

        state.ShowCurrentMapOnProjector();

        Assert.Equal(1, changes);
        Assert.Equal("level-2", state.ProjectorMapId);
        Assert.False(state.ProjectorOnAnotherMap);
        ProjectorView projector = state.Projector();
        Assert.Equal("level-2", projector.MapId);
        Assert.True(projector.IsCurrentMap);
        Assert.Equal(RoomNine, projector.RevealedRegionIds);
        Assert.Contains((7, 7), projector.FogCells);
        Assert.Equal(LowerFraming, projector.Viewport);

        // Shown already: nothing to do, and nothing to tell the views.
        state.ShowCurrentMapOnProjector();
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Showing_before_the_table_is_on_any_map_does_nothing()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.ShowCurrentMapOnProjector();

        Assert.Equal(0, changes);
        Assert.Equal(string.Empty, state.ProjectorMapId);
    }

    [Fact]
    public void Going_back_to_the_map_on_the_projector_ends_the_hold()
    {
        SessionState state = DownAFloor();

        state.SetCurrentMap("level-1");
        state.ToggleRegion("room-2");

        Assert.False(state.ProjectorOnAnotherMap);
        ProjectorView projector = state.Projector();
        Assert.True(projector.IsCurrentMap);
        // Live again: a room revealed now is on the projector at once.
        Assert.Contains("room-2", projector.RevealedRegionIds);
        Assert.Contains("room-1", projector.RevealedRegionIds);
    }

    [Fact]
    public void A_point_on_the_map_the_players_cannot_see_is_kept_off_the_projector_and_dropped_when_it_is_shown()
    {
        SessionState state = DownAFloor();

        state.PingAt(600, 600);

        // The DM's own map marks where they pointed; the projector, on another map, does not.
        Assert.True(state.Ping.IsSet);
        Assert.False(state.Projector().Ping.IsSet);

        state.ShowCurrentMapOnProjector();

        Assert.False(state.Ping.IsSet);
        Assert.False(state.Projector().Ping.IsSet);
    }

    [Fact]
    public void A_point_on_the_map_the_players_see_is_on_the_projector()
    {
        var state = new SessionState();
        state.SetCurrentMap("level-1");

        state.PingAt(120, 340);

        PingMarker ping = state.Projector().Ping;
        Assert.True(ping.IsSet);
        Assert.Equal(120, ping.X);
        Assert.Equal(340, ping.Y);
    }

    [Fact]
    public void Turning_the_projector_turns_the_view_it_is_holding()
    {
        var state = new SessionState();
        state.SetCurrentMap("level-1");
        state.MarkSeeded("level-1");
        state.SetPlayerViewport(new MapBounds(0, 0, 160, 90));
        state.SetCurrentMap("level-2");

        state.TurnPlayerView();

        Assert.Equal(new MapBounds(35, -35, 125, 125), state.Projector().Viewport);
    }

    [Fact]
    public void A_map_held_with_nothing_on_it_shows_dark()
    {
        // A map never set up and with nothing on it is not kept when the table leaves it (SessionState
        // .SetAside), so there is nothing to show of it: the projector stays on it, all fog.
        var state = new SessionState();
        state.SetCurrentMap("level-1");
        state.SetCurrentMap("level-2");
        state.ToggleRegion("room-9");

        ProjectorView projector = state.Projector();

        Assert.Equal("level-1", projector.MapId);
        Assert.Empty(projector.RevealedRegionIds);
        Assert.Empty(projector.FogCells);
        Assert.Equal(new MapBounds(), projector.Viewport);
    }

    [Fact]
    public void The_map_on_the_projector_survives_a_capture_and_restore()
    {
        RevealSnapshot saved = DownAFloor().Capture();

        var restored = new SessionState();
        restored.Restore(saved);

        Assert.Equal("level-2", restored.CurrentMapId);
        Assert.Equal("level-1", restored.ProjectorMapId);
        ProjectorView projector = restored.Projector();
        Assert.Equal(RoomOne, projector.RevealedRegionIds);
        Assert.Equal(UpperFraming, projector.Viewport);
    }

    [Fact]
    public void A_save_from_before_the_projector_could_stay_behind_shows_the_map_in_play()
    {
        RevealSnapshot saved = DownAFloor().Capture();
        saved.ProjectorMapId = string.Empty;

        var restored = new SessionState();
        restored.Restore(saved);

        Assert.Equal("level-2", restored.ProjectorMapId);
        Assert.False(restored.ProjectorOnAnotherMap);
        Assert.Equal(RoomNine, restored.Projector().RevealedRegionIds);
    }

    // The party was on level 1, with a room, a trap, a cell and a door showing and the projector framed,
    // and the DM has gone down to level 2 and set up its first room and framing there.
    private static SessionState DownAFloor()
    {
        var state = new SessionState();
        state.SetCurrentMap("level-1");
        state.MarkSeeded("level-1");
        state.ToggleRegion("room-1");
        state.ToggleFeature("trap-1");
        state.PaintCell(4, 9, erase: false);
        state.SetDoorOpen("door-1", open: true);
        state.SetPlayerViewport(UpperFraming);

        state.SetCurrentMap("level-2");
        state.MarkSeeded("level-2");
        state.ToggleRegion("room-9");
        state.PaintCell(7, 7, erase: false);
        state.SetPlayerViewport(LowerFraming);
        return state;
    }
}
