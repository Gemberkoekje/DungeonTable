using System.Threading.Tasks;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The dock follows the party: walking the token into a room shows that room on the Room tab, looking at
/// another room lasts until the party moves on, and a pinned dock stays where the DM put it.
/// </summary>
public sealed class DmWorkspacePartyTests
{
    [Fact]
    public async Task The_party_is_in_the_area_of_the_region_under_its_token()
    {
        PartyHarness harness = await PartyHarness.Started();
        Assert.Equal(string.Empty, harness.Workspace.PartyArea);

        harness.Moves.Move(85, 25, PartyMoveKind.Place);
        Assert.Equal(PartyHarness.AreaB, harness.Workspace.PartyArea);

        // The corridor's region opens no area, and a region linked to a creature is not a room.
        harness.Moves.Move(25, 105, PartyMoveKind.Place);
        Assert.Equal(string.Empty, harness.Workspace.PartyArea);
        harness.Moves.Move(155, 105, PartyMoveKind.Place);
        Assert.Equal(string.Empty, harness.Workspace.PartyArea);
    }

    [Fact]
    public async Task Walking_the_party_into_another_room_shows_that_room()
    {
        PartyHarness harness = await PartyHarness.Started();
        harness.Moves.Move(25, 25, PartyMoveKind.Place);
        harness.Workspace.FollowParty();
        Assert.Equal(PartyHarness.AreaA, harness.Workspace.PanelNode);

        harness.Moves.Move(85, 25, PartyMoveKind.Walk);
        harness.Workspace.FollowParty();

        Assert.Equal(PartyHarness.AreaB, harness.Workspace.PanelNode);
        Assert.False(harness.Workspace.LookingAway);
    }

    [Fact]
    public async Task Looking_at_another_room_lasts_while_the_party_stays_in_its_own()
    {
        PartyHarness harness = await PartyHarness.Started();
        harness.Moves.Move(25, 25, PartyMoveKind.Place);
        harness.Workspace.FollowParty();

        harness.Workspace.NavigateTo(PartyHarness.AreaB);
        Assert.True(harness.Workspace.LookingAway);

        // A step within the Landing is not a new room: the DM keeps reading the Nave.
        harness.Moves.Move(45, 25, PartyMoveKind.Walk);
        harness.Workspace.FollowParty();

        Assert.Equal(PartyHarness.AreaB, harness.Workspace.PanelNode);
        Assert.True(harness.Workspace.LookingAway);
    }

    [Fact]
    public async Task Looking_away_ends_when_the_party_walks_into_another_room()
    {
        PartyHarness harness = await PartyHarness.Started();
        harness.Moves.Move(85, 25, PartyMoveKind.Place);
        harness.Workspace.FollowParty();
        harness.Workspace.NavigateTo(PartyHarness.AreaB);
        harness.Workspace.NavigateTo(PartyHarness.AreaA);

        // The DM reads the Landing while the party is in the Nave; the party comes to the Landing, then
        // goes back to the Nave, and the dock goes with it. The shell follows after every change.
        harness.Moves.Move(25, 25, PartyMoveKind.Walk);
        harness.Workspace.FollowParty();
        Assert.Equal(PartyHarness.AreaA, harness.Workspace.PanelNode);

        harness.Moves.Move(85, 25, PartyMoveKind.Walk);
        harness.Workspace.FollowParty();

        Assert.Equal(PartyHarness.AreaB, harness.Workspace.PanelNode);
    }

    [Fact]
    public async Task A_pinned_dock_stays_when_the_party_moves_and_going_back_unpins_it()
    {
        PartyHarness harness = await PartyHarness.Started();
        harness.Moves.Move(25, 25, PartyMoveKind.Place);
        harness.Workspace.FollowParty();
        harness.Workspace.TogglePin();

        harness.Moves.Move(85, 25, PartyMoveKind.Walk);
        harness.Workspace.FollowParty();

        Assert.Equal(PartyHarness.AreaA, harness.Workspace.PanelNode);
        Assert.True(harness.Workspace.LookingAway);

        harness.Workspace.BackToParty();

        Assert.Equal(PartyHarness.AreaB, harness.Workspace.PanelNode);
        Assert.False(harness.Workspace.Pinned);
    }

    [Fact]
    public async Task Undoing_a_move_brings_the_dock_back_with_the_party()
    {
        PartyHarness harness = await PartyHarness.Started();
        harness.Moves.Move(25, 25, PartyMoveKind.Place);
        harness.Workspace.FollowParty();
        harness.Moves.Move(85, 25, PartyMoveKind.Walk);
        harness.Workspace.FollowParty();

        harness.Session.UndoPartyMove();
        harness.Workspace.FollowParty();

        Assert.Equal(PartyHarness.AreaA, harness.Workspace.PanelNode);
    }

    [Fact]
    public async Task A_dm_window_opened_on_a_table_in_play_opens_on_the_party_s_room()
    {
        PartyHarness first = await PartyHarness.Started();
        Assert.Equal(PartyHarness.AreaA, first.Workspace.PanelNode);

        PartyHarness reopened = await PartyHarness.StartedWithPartyAt(85, 25);

        Assert.Equal(PartyHarness.AreaB, reopened.Workspace.PanelNode);
    }
}
