using System.Threading.Tasks;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Moving the party token reveals what the party sees: from the square it is put in, from every square
/// a drag walks it through, and again when a door opens or the range grows. The rule is the old
/// lifter's, so these check where it is run from, on the map <see cref="PartyHarness"/> describes.
/// </summary>
public sealed class PartyMovesTests
{
    [Fact]
    public async Task Putting_the_party_down_reveals_what_it_sees_from_there_and_snaps_it_to_its_square()
    {
        PartyHarness harness = await PartyHarness.Started();

        harness.Moves.Move(23, 27, PartyMoveKind.Place);

        Assert.Equal(25, harness.Session.Party.X);
        Assert.Equal(25, harness.Session.Party.Y);
        Assert.Contains((2, 2), harness.Session.FogCells());
        Assert.Contains((0, 0), harness.Session.FogCells());

        // The Nave's door is shut, and the corridor is out of a 30 ft range.
        Assert.DoesNotContain((7, 2), harness.Session.FogCells());
        Assert.DoesNotContain((2, 10), harness.Session.FogCells());
    }

    [Fact]
    public async Task A_token_put_off_the_map_stands_on_its_edge()
    {
        PartyHarness harness = await PartyHarness.Started();

        harness.Moves.Move(-50, 900, PartyMoveKind.Place);

        Assert.Equal(5, harness.Session.Party.X);
        Assert.Equal(155, harness.Session.Party.Y);
    }

    [Fact]
    public async Task A_walk_reveals_what_the_party_saw_on_the_way_and_a_jump_does_not()
    {
        // The alcove at column 9 is within a 10 ft range only from the middle of the corridor.
        PartyHarness walked = await PartyHarness.Started();
        walked.Session.SetPartyVision(10);
        (double startX, double startY) = PartyHarness.Middle(0, 10);
        (double endX, double endY) = PartyHarness.Middle(19, 10);
        walked.Moves.Move(startX, startY, PartyMoveKind.Place);

        walked.Moves.Move(endX, endY, PartyMoveKind.Walk);

        Assert.Contains((9, 12), walked.Session.FogCells());

        PartyHarness jumped = await PartyHarness.Started();
        jumped.Session.SetPartyVision(10);
        jumped.Moves.Move(startX, startY, PartyMoveKind.Place);

        jumped.Moves.Move(endX, endY, PartyMoveKind.Place);

        Assert.DoesNotContain((9, 12), jumped.Session.FogCells());
    }

    [Fact]
    public async Task A_drag_reported_in_pieces_is_taken_back_as_one_move()
    {
        PartyHarness harness = await PartyHarness.Started();
        harness.Session.SetPartyVision(10);
        (double startX, double startY) = PartyHarness.Middle(0, 10);
        harness.Moves.Move(startX, startY, PartyMoveKind.Place);
        int before = harness.Session.FogCells().Count;

        harness.Moves.Move(PartyHarness.Middle(6, 10).X, startY, PartyMoveKind.Walk);
        harness.Moves.Move(PartyHarness.Middle(12, 10).X, startY, PartyMoveKind.WalkOn);
        harness.Moves.Move(PartyHarness.Middle(19, 10).X, startY, PartyMoveKind.WalkOn);
        harness.Session.UndoPartyMove();

        Assert.Equal(startX, harness.Session.Party.X);
        Assert.Equal(before, harness.Session.FogCells().Count);
    }

    [Fact]
    public async Task Opening_a_door_shows_the_room_beyond_and_undo_shuts_it_again()
    {
        PartyHarness harness = await PartyHarness.Started();
        harness.Moves.Move(25, 25, PartyMoveKind.Place);
        DoorState before = harness.Session.DoorStateOf(PartyHarness.NaveDoor);

        harness.Session.SetDoorOpen(PartyHarness.NaveDoor, open: true);
        harness.Moves.AfterDoor(before);

        Assert.Contains((7, 2), harness.Session.FogCells());

        harness.Session.UndoPartyMove();

        Assert.DoesNotContain((7, 2), harness.Session.FogCells());
        Assert.Empty(harness.Session.OpenDoorIds());
        Assert.Contains((2, 2), harness.Session.FogCells());
    }

    [Fact]
    public async Task A_secret_door_hides_the_room_behind_it_until_the_players_find_it()
    {
        PartyHarness harness = await PartyHarness.Started();
        harness.Moves.Move(95, 25, PartyMoveKind.Place);
        Assert.DoesNotContain((12, 2), harness.Session.FogCells());

        DoorState before = harness.Session.DoorStateOf(PartyHarness.SecretDoor);
        harness.Session.ToggleFeature(PartyHarness.SecretDoor);
        harness.Moves.AfterDoor(before);

        Assert.Contains((12, 2), harness.Session.FogCells());
    }

    [Fact]
    public async Task A_longer_range_reveals_straight_away_what_it_reaches()
    {
        PartyHarness harness = await PartyHarness.Started();
        harness.Session.SetPartyVision(10);
        (double x, double y) = PartyHarness.Middle(0, 10);
        harness.Moves.Move(x, y, PartyMoveKind.Place);
        Assert.DoesNotContain((5, 10), harness.Session.FogCells());

        harness.Moves.SetVision(30);

        Assert.Equal(30, harness.Session.Party.VisionFeet);
        Assert.Contains((5, 10), harness.Session.FogCells());
    }

    [Fact]
    public async Task A_door_with_no_party_on_the_map_reveals_nothing()
    {
        PartyHarness harness = await PartyHarness.Started();
        DoorState before = harness.Session.DoorStateOf(PartyHarness.NaveDoor);

        harness.Session.SetDoorOpen(PartyHarness.NaveDoor, open: true);
        harness.Moves.AfterDoor(before);

        Assert.Empty(harness.Session.FogCells());
        Assert.False(harness.Session.CanUndoPartyMove);
    }

    [Fact]
    public async Task The_projector_follows_the_party_once_it_leaves_the_middle_of_the_view()
    {
        PartyHarness harness = await PartyHarness.Started();
        harness.Moves.Move(25, 25, PartyMoveKind.Place);
        var view = new MapBounds(0, 0, 80, 45);
        harness.Session.SetPlayerViewport(view);
        harness.Moves.SetProjectorFollows(true);

        // Turning it on with the party in the middle leaves the projector be.
        Assert.Equal(view, harness.Session.PlayerViewport);

        harness.Moves.Move(155, 25, PartyMoveKind.Walk);

        MapBounds followed = harness.Session.PlayerViewport;
        Assert.Equal(80, followed.Width, 6);
        Assert.Equal(155, followed.MinX + (followed.Width / 2), 6);
    }

    [Fact]
    public async Task The_projector_stays_put_while_following_is_off()
    {
        PartyHarness harness = await PartyHarness.Started();
        var view = new MapBounds(0, 0, 80, 45);
        harness.Session.SetPlayerViewport(view);

        harness.Moves.Move(155, 25, PartyMoveKind.Place);

        Assert.Equal(view, harness.Session.PlayerViewport);
    }

    [Fact]
    public async Task The_ring_round_the_token_is_its_range_in_world_units()
    {
        PartyHarness harness = await PartyHarness.Started();

        harness.Session.SetPartyVision(60);

        Assert.Equal(120, harness.Moves.VisionRadius, 6);
    }
}
