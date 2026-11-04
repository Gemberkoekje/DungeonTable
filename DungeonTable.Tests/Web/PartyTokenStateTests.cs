using System.Collections.Generic;
using DungeonTable.Core.Session;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The party token on the shared session: where it stands, how far the party sees, what each move
/// revealed, and taking a move back with Ctrl+Z, a whole drag at a time, the door it came from included.
/// </summary>
public sealed class PartyTokenStateTests
{
    private static readonly (int Col, int Row)[] LandingCells = { (1, 1), (2, 1) };
    private static readonly (int Col, int Row)[] PassageCells = { (2, 1), (3, 1) };
    private static readonly (int Col, int Row)[] DenCells = { (4, 1), (5, 1) };
    private static readonly (int Col, int Row)[] BeyondTheDoor = { (7, 7) };
    private static readonly (int Col, int Row)[] OneCell = { (1, 1) };

    [Fact]
    public void A_table_starts_with_no_token_and_the_default_vision()
    {
        var state = new SessionState();

        Assert.False(state.Party.IsPlaced);
        Assert.Equal(30, state.Party.VisionFeet);
        Assert.False(state.CanUndoPartyMove);
    }

    [Fact]
    public void Moving_the_party_puts_the_token_down_and_reveals_what_it_saw()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.MoveParty(15, 25, LandingCells, joinLast: false);

        Assert.True(state.Party.IsPlaced);
        Assert.Equal(15, state.Party.X);
        Assert.Equal(25, state.Party.Y);
        Assert.Equal(LandingCells, Sorted(state.FogCells()));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Undo_takes_a_whole_drag_back_and_puts_the_token_where_it_stood_before_it()
    {
        var state = new SessionState();
        state.MoveParty(15, 15, LandingCells, joinLast: false);

        // A drag: its first report starts a move, the rest join it.
        state.MoveParty(35, 15, PassageCells, joinLast: false);
        state.MoveParty(55, 15, DenCells, joinLast: true);

        state.UndoPartyMove();

        Assert.Equal(15, state.Party.X);
        Assert.Equal(15, state.Party.Y);
        Assert.Equal(LandingCells, Sorted(state.FogCells()));

        // One more step back takes the token off the map, as it was before the first move.
        state.UndoPartyMove();

        Assert.False(state.Party.IsPlaced);
        Assert.Empty(state.FogCells());
        Assert.False(state.CanUndoPartyMove);
    }

    [Fact]
    public void A_drag_let_go_where_it_started_is_nothing_to_take_back()
    {
        var state = new SessionState();
        state.MoveParty(15, 15, LandingCells, joinLast: false);

        // The drag never left its square: one report, to where the token already stands.
        state.MoveParty(15, 15, LandingCells, joinLast: false);
        state.UndoPartyMove();

        Assert.False(state.Party.IsPlaced);
        Assert.False(state.CanUndoPartyMove);
    }

    [Fact]
    public void Undo_hides_only_what_the_move_revealed_not_what_was_showing_already()
    {
        var state = new SessionState();
        state.PaintCells(OneCell);

        state.MoveParty(15, 15, LandingCells, joinLast: false);
        state.UndoPartyMove();

        Assert.Equal(OneCell, Sorted(state.FogCells()));
    }

    [Fact]
    public void What_an_opened_door_showed_is_taken_back_with_the_door()
    {
        var state = new SessionState();
        state.MoveParty(15, 15, LandingCells, joinLast: false);
        DoorState before = state.DoorStateOf("door-nave");
        state.SetDoorOpen("door-nave", open: true);

        state.RevealFromParty(BeyondTheDoor, before);
        Assert.Contains((7, 7), state.FogCells());

        state.UndoPartyMove();

        Assert.DoesNotContain((7, 7), state.FogCells());
        Assert.Empty(state.OpenDoorIds());
        Assert.True(state.Party.IsPlaced);
        Assert.Equal(15, state.Party.X);
    }

    [Fact]
    public void Taking_back_a_found_secret_door_hides_it_again()
    {
        var state = new SessionState();
        state.MoveParty(15, 15, LandingCells, joinLast: false);
        DoorState before = state.DoorStateOf("secret-door");
        state.ToggleFeature("secret-door");

        state.RevealFromParty(BeyondTheDoor, before);
        state.UndoPartyMove();

        Assert.Empty(state.RevealedFeatureIds());
    }

    [Fact]
    public void A_look_that_showed_nothing_new_is_not_something_to_take_back()
    {
        var state = new SessionState();
        state.MoveParty(15, 15, LandingCells, joinLast: false);
        int changes = 0;
        state.Changed += () => changes++;

        // Had this look been kept, taking it back would open the door it names.
        state.RevealFromParty(LandingCells, new DoorState("door-1", true, false));

        Assert.Equal(0, changes);
        state.UndoPartyMove();
        Assert.False(state.CanUndoPartyMove);
        Assert.Empty(state.OpenDoorIds());
        Assert.Empty(state.FogCells());
    }

    [Fact]
    public void Ctrl_z_remembers_the_last_fifty_moves()
    {
        var state = new SessionState();
        for (int i = 0; i < 51; i++)
        {
            state.MoveParty(i, 0, new[] { (i, 0) }, joinLast: false);
        }

        for (int i = 0; i < 50; i++)
        {
            state.UndoPartyMove();
        }

        Assert.False(state.CanUndoPartyMove);
        Assert.Equal(0, state.Party.X);
        Assert.Equal((0, 0), Assert.Single(state.FogCells()));
    }

    [Theory]
    [InlineData(60, 60)]
    [InlineData(1, 5)]
    [InlineData(500, 120)]
    [InlineData(0, 30)]
    [InlineData(-10, 30)]
    public void The_vision_range_is_kept_in_bounds(int asked, int kept)
    {
        var state = new SessionState();

        state.SetPartyVision(asked);

        Assert.Equal(kept, state.Party.VisionFeet);
    }

    [Fact]
    public void Undo_leaves_the_vision_range_as_last_set()
    {
        var state = new SessionState();
        state.MoveParty(15, 15, LandingCells, joinLast: false);
        state.SetPartyVision(60);

        state.UndoPartyMove();

        Assert.Equal(60, state.Party.VisionFeet);
    }

    [Fact]
    public void Taking_the_token_off_keeps_what_it_revealed_and_its_vision()
    {
        var state = new SessionState();
        state.SetPartyVision(60);
        state.MoveParty(15, 15, LandingCells, joinLast: false);

        state.RemoveParty();

        Assert.False(state.Party.IsPlaced);
        Assert.Equal(60, state.Party.VisionFeet);
        Assert.Equal(LandingCells, Sorted(state.FogCells()));
    }

    [Fact]
    public void Each_map_keeps_its_own_token_and_a_map_switch_drops_the_undo()
    {
        var state = new SessionState();
        state.SetCurrentMap("level-1");
        state.SetPartyVision(60);
        state.MoveParty(15, 15, LandingCells, joinLast: false);

        state.SetCurrentMap("level-2");

        Assert.False(state.Party.IsPlaced);
        Assert.Equal(30, state.Party.VisionFeet);
        Assert.False(state.CanUndoPartyMove);

        state.SetCurrentMap("level-1");

        Assert.True(state.Party.IsPlaced);
        Assert.Equal(15, state.Party.X);
        Assert.Equal(60, state.Party.VisionFeet);
        Assert.False(state.CanUndoPartyMove);
    }

    [Fact]
    public void A_map_with_only_the_token_on_it_is_still_set_aside()
    {
        var state = new SessionState();
        state.SetCurrentMap("level-1");
        state.MoveParty(15, 15, Array.Empty<(int, int)>(), joinLast: false);

        state.SetCurrentMap("level-2");
        state.SetCurrentMap("level-1");

        Assert.True(state.Party.IsPlaced);
    }

    [Fact]
    public void Clearing_the_reveals_leaves_the_token_and_drops_what_ctrl_z_would_take_back()
    {
        var state = new SessionState();
        state.MoveParty(15, 15, LandingCells, joinLast: false);

        state.ClearReveals();

        Assert.True(state.Party.IsPlaced);
        Assert.False(state.CanUndoPartyMove);
    }

    [Fact]
    public void Following_with_the_projector_is_a_setting_that_reports_only_a_change()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.SetProjectorFollowsParty(true);
        state.SetProjectorFollowsParty(true);

        Assert.True(state.ProjectorFollowsParty);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void A_position_that_is_not_a_number_is_ignored()
    {
        var state = new SessionState();

        state.MoveParty(double.NaN, 15, LandingCells, joinLast: false);

        Assert.False(state.Party.IsPlaced);
        Assert.Empty(state.FogCells());
    }

    private static (int Col, int Row)[] Sorted(IEnumerable<(int Col, int Row)> cells) =>
        cells.OrderBy(cell => cell.Col).ThenBy(cell => cell.Row).ToArray();
}
