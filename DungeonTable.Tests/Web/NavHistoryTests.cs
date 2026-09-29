using DungeonTable.Core.Dossier;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Verifies the DM's browser-like reference history: appending stops, moving the cursor with
/// Back / Forward / breadcrumb jumps, branching over a stale forward trail, and finding the room
/// the briefing panel should stay on while a reference card is open.
/// </summary>
public sealed class NavHistoryTests
{
    [Fact]
    public void A_new_history_has_no_current_stop_and_cannot_move()
    {
        var history = new NavHistory();

        Assert.Equal(-1, history.Cursor);
        Assert.False(history.CanBack);
        Assert.False(history.CanForward);
        Assert.False(history.Back());
        Assert.False(history.Forward());
        Assert.Equal(string.Empty, history.NearestArea());
    }

    [Fact]
    public void Pushing_stops_moves_the_cursor_to_the_newest()
    {
        var history = new NavHistory();

        Assert.True(history.Push(Area("data_dossiers_level_1_area_1")));
        Assert.True(history.Push(Monster("data_statblocks_monsters_bugbear")));

        Assert.Equal(2, history.Entries.Count);
        Assert.Equal(1, history.Cursor);
        Assert.Equal("data_statblocks_monsters_bugbear", history.Current.NodeId);
    }

    [Fact]
    public void Re_pushing_the_current_stop_is_ignored()
    {
        var history = new NavHistory();
        history.Push(Area("data_dossiers_level_1_area_1"));

        Assert.False(history.Push(Area("data_dossiers_level_1_area_1")));
        Assert.Single(history.Entries);
    }

    [Fact]
    public void The_same_node_as_a_different_kind_is_a_new_stop()
    {
        var history = new NavHistory();
        history.Push(Area("data_dossiers_level_1_flesh_golem"));

        Assert.True(history.Push(Monster("data_dossiers_level_1_flesh_golem")));
        Assert.Equal(2, history.Entries.Count);
    }

    [Fact]
    public void Back_and_forward_walk_the_trail_without_losing_stops()
    {
        var history = new NavHistory();
        history.Push(Area("a"));
        history.Push(Area("b"));
        history.Push(Area("c"));

        Assert.True(history.Back());
        Assert.True(history.Back());
        Assert.Equal("a", history.Current.NodeId);
        Assert.False(history.CanBack);
        Assert.True(history.CanForward);

        Assert.True(history.Forward());
        Assert.Equal("b", history.Current.NodeId);
        Assert.Equal(3, history.Entries.Count);
    }

    [Fact]
    public void Pushing_after_going_back_drops_the_stale_forward_trail()
    {
        var history = new NavHistory();
        history.Push(Area("a"));
        history.Push(Area("b"));
        history.Push(Area("c"));
        history.Back();

        history.Push(Area("d"));

        Assert.Equal(BranchedTrail, history.Entries.Select(entry => entry.NodeId).ToArray());
        Assert.Equal(2, history.Cursor);
        Assert.False(history.CanForward);
    }

    [Fact]
    public void Jumping_to_an_out_of_range_index_leaves_the_cursor_alone()
    {
        var history = new NavHistory();
        history.Push(Area("a"));
        history.Push(Area("b"));

        Assert.False(history.JumpTo(-1));
        Assert.False(history.JumpTo(2));
        Assert.Equal(1, history.Cursor);

        Assert.True(history.JumpTo(0));
        Assert.Equal("a", history.Current.NodeId);
    }

    [Fact]
    public void The_nearest_area_is_the_room_the_panel_stays_on_under_a_card()
    {
        var history = new NavHistory();
        history.Push(Area("data_dossiers_level_1_area_1"));
        history.Push(Monster("data_statblocks_monsters_bugbear"));
        history.Push(Spell("data_statblocks_spells_fireball"));

        // Two entity stops deep, the panel still shows the room they were reached from.
        Assert.Equal("data_dossiers_level_1_area_1", history.NearestArea());
        Assert.Equal(0, history.NearestAreaIndex());
    }

    [Fact]
    public void An_entity_only_history_has_no_nearest_area()
    {
        var history = new NavHistory();
        history.Push(Monster("data_statblocks_monsters_bugbear"));

        Assert.Equal(-1, history.NearestAreaIndex());
        Assert.Equal(string.Empty, history.NearestArea());
    }

    private static readonly string[] BranchedTrail = { "a", "b", "d" };

    private static NavEntry Area(string node) => new NavEntry(node, CrossRefKind.Area, node);

    private static NavEntry Monster(string node) => new NavEntry(node, CrossRefKind.Monster, node);

    private static NavEntry Spell(string node) => new NavEntry(node, CrossRefKind.Spell, node);
}
