using System.Collections.Generic;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Verifies the shared DM/player session state stores the player viewport and aspect and raises
/// <see cref="SessionState.Changed"/> only on real changes.
/// </summary>
public sealed class SessionStateTests
{
    [Fact]
    public void Setting_the_viewport_stores_it_and_raises_changed_once()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.SetPlayerViewport(new MapBounds(10, 20, 110, 80));

        Assert.Equal(new MapBounds(10, 20, 110, 80), state.PlayerViewport);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Setting_a_new_aspect_updates_it_and_raises_changed()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.SetPlayerAspect(4.0 / 3.0);

        Assert.Equal(4.0 / 3.0, state.PlayerAspect, 4);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Non_positive_or_unchanged_aspect_is_ignored()
    {
        var state = new SessionState();
        double original = state.PlayerAspect;
        int changes = 0;
        state.Changed += () => changes++;

        state.SetPlayerAspect(0);
        state.SetPlayerAspect(-2);
        state.SetPlayerAspect(original); // same value, within the dead-band

        Assert.Equal(original, state.PlayerAspect, 6);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Toggling_a_region_reveals_then_hides_it()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.ToggleRegion("room-1");
        Assert.Contains("room-1", state.RevealedRegionIds());

        state.ToggleRegion("room-1");
        Assert.DoesNotContain("room-1", state.RevealedRegionIds());
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Toggling_an_empty_feature_id_is_ignored()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.ToggleFeature(string.Empty);

        Assert.Empty(state.RevealedFeatureIds());
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Painting_a_cell_adds_it_and_erasing_removes_it()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.PaintCell(3, 4, erase: false);
        Assert.Contains((3, 4), state.FogCells());

        // Painting the same cell again is a no-op: the set is unchanged, so no event fires.
        state.PaintCell(3, 4, erase: false);
        Assert.Equal(1, changes);

        state.PaintCell(3, 4, erase: true);
        Assert.DoesNotContain((3, 4), state.FogCells());
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Switching_to_a_different_map_clears_all_reveals()
    {
        var state = new SessionState();
        state.SetCurrentMap("level-1");
        state.ToggleRegion("room-1");
        state.ToggleFeature("door-1");
        state.PaintCell(1, 1, erase: false);

        state.SetCurrentMap("level-2");

        Assert.Equal("level-2", state.CurrentMapId);
        Assert.Empty(state.RevealedRegionIds());
        Assert.Empty(state.RevealedFeatureIds());
        Assert.Empty(state.FogCells());
    }

    [Fact]
    public void Re_selecting_the_same_map_keeps_the_reveal_state()
    {
        var state = new SessionState();
        state.SetCurrentMap("level-1");
        state.ToggleRegion("room-1");

        state.SetCurrentMap("level-1");

        Assert.Contains("room-1", state.RevealedRegionIds());
    }

    [Fact]
    public void Clear_reveals_hides_everything_and_raises_changed_when_there_was_state()
    {
        var state = new SessionState();
        state.ToggleRegion("room-1");
        int changes = 0;
        state.Changed += () => changes++;

        state.ClearReveals();
        Assert.Empty(state.RevealedRegionIds());
        Assert.Equal(1, changes);

        // Clearing an already-empty state changes nothing, so no event fires.
        state.ClearReveals();
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Clearing_can_be_undone_once_and_restores_everything_it_hid()
    {
        var state = new SessionState();
        state.ToggleRegion("room-1");
        state.ToggleFeature("door-1");
        state.PaintCell(2, 3, erase: false);

        state.ClearReveals();
        Assert.True(state.CanUndoClearReveals);

        state.UndoClearReveals();

        Assert.Contains("room-1", state.RevealedRegionIds());
        Assert.Contains("door-1", state.RevealedFeatureIds());
        Assert.Contains((2, 3), state.FogCells());

        // The snapshot is one step deep, and this call spent it.
        Assert.False(state.CanUndoClearReveals);
    }

    [Fact]
    public void Undoing_a_clear_keeps_whatever_was_revealed_since()
    {
        var state = new SessionState();
        state.ToggleRegion("room-1");
        state.ClearReveals();
        state.ToggleRegion("room-2");

        state.UndoClearReveals();

        Assert.Equal(2, state.RevealedRegionIds().Count);
    }

    [Fact]
    public void Clearing_nothing_leaves_no_undo_and_undoing_nothing_is_silent()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.ClearReveals();
        Assert.False(state.CanUndoClearReveals);

        state.UndoClearReveals();
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Switching_maps_drops_the_undo_snapshot()
    {
        var state = new SessionState();
        state.SetCurrentMap("level-1");
        state.ToggleRegion("room-1");
        state.ClearReveals();

        state.SetCurrentMap("level-2");

        // Restoring level 1's region ids onto level 2 would reveal geometry that isn't there.
        Assert.False(state.CanUndoClearReveals);
        state.UndoClearReveals();
        Assert.Empty(state.RevealedRegionIds());
    }

    [Fact]
    public void Painting_a_batch_of_cells_reveals_or_erases_them_together()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.PaintCells(BrushBlock, erase: false);
        Assert.Equal(3, state.FogCells().Count);
        Assert.Equal(1, changes);

        // Erasing the same block empties the mask again, in one notification.
        state.PaintCells(BrushBlock, erase: true);
        Assert.Empty(state.FogCells());
        Assert.Equal(2, changes);

        // Erasing what is already hidden changes nothing, so no event fires.
        state.PaintCells(BrushBlock, erase: true);
        Assert.Equal(2, changes);
    }

    private static readonly (int Col, int Row)[] BrushBlock = { (0, 0), (0, 1), (1, 0) };

    [Fact]
    public void Snapshot_accessors_return_independent_copies()
    {
        var state = new SessionState();
        state.ToggleRegion("room-1");

        IReadOnlyCollection<string> snapshot = state.RevealedRegionIds();
        state.ToggleRegion("room-2"); // mutate after the snapshot was taken

        Assert.Single(snapshot);          // the earlier snapshot is unaffected
        Assert.Equal(2, state.RevealedRegionIds().Count);
    }

    [Fact]
    public void Toggling_a_door_open_and_closed_tracks_and_notifies()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.ToggleDoorOpen("door-1");
        Assert.Contains("door-1", state.OpenDoorIds());

        state.ToggleDoorOpen("door-1");
        Assert.DoesNotContain("door-1", state.OpenDoorIds());
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Seeding_open_doors_replaces_the_set()
    {
        var state = new SessionState();
        state.SeedOpenDoors(SeedDoorsAb);
        Assert.Equal(2, state.OpenDoorIds().Count);

        state.SeedOpenDoors(SeedDoorC);
        Assert.Contains("c", state.OpenDoorIds());
        Assert.DoesNotContain("a", state.OpenDoorIds());
    }

    private static readonly string[] SeedDoorsAb = { "a", "b" };
    private static readonly string[] SeedDoorC = { "c" };

    [Fact]
    public void Switching_maps_clears_open_doors()
    {
        var state = new SessionState();
        state.SetCurrentMap("m1");
        state.ToggleDoorOpen("door-1");

        state.SetCurrentMap("m2");

        Assert.Empty(state.OpenDoorIds());
    }

    [Fact]
    public void A_non_finite_or_absurd_player_aspect_is_rejected_or_clamped()
    {
        var state = new SessionState();
        double original = state.PlayerAspect;

        state.SetPlayerAspect(double.PositiveInfinity);
        Assert.Equal(original, state.PlayerAspect); // non-finite ignored

        state.SetPlayerAspect(double.NaN);
        Assert.Equal(original, state.PlayerAspect); // NaN ignored

        state.SetPlayerAspect(-2);
        Assert.Equal(original, state.PlayerAspect); // non-positive ignored

        state.SetPlayerAspect(1000);
        Assert.True(state.PlayerAspect <= 5.0 && state.PlayerAspect >= 0.2); // absurd value clamped
    }

    // ---- Pointing at the projector -----------------------------------------------------------

    [Fact]
    public void Pointing_twice_at_the_same_spot_issues_a_new_ping()
    {
        // The pulse only replays when the sequence changes, so a DM pointing at the same doorway
        // again to make sure the players saw it must produce a different number, not a no-op.
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.PingAt(120, 340);
        long first = state.Ping.Sequence;
        state.PingAt(120, 340);

        Assert.True(state.Ping.IsSet);
        Assert.Equal(120, state.Ping.X);
        Assert.Equal(340, state.Ping.Y);
        Assert.True(state.Ping.Sequence > first);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void A_non_finite_ping_is_ignored()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.PingAt(double.NaN, 10);
        state.PingAt(10, double.PositiveInfinity);

        Assert.False(state.Ping.IsSet);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Clearing_the_pointer_takes_it_down_and_only_notifies_when_there_was_one()
    {
        var state = new SessionState();
        state.PingAt(50, 60);
        int changes = 0;
        state.Changed += () => changes++;

        state.ClearPing();
        state.ClearPing();

        Assert.False(state.Ping.IsSet);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Switching_map_drops_the_pointer()
    {
        // A ping is a point in one map's world coordinates; carried over it would land somewhere
        // arbitrary on the next floor.
        var state = new SessionState();
        state.SetCurrentMap("level-1");
        state.PingAt(50, 60);

        state.SetCurrentMap("level-2");

        Assert.False(state.Ping.IsSet);
    }

    [Fact]
    public void The_pointer_is_not_part_of_what_is_saved()
    {
        // Deliberate: a ping means "look here, now". One restored from a previous session would be
        // a stale hand pointing at nothing, so Capture/Restore leave it out entirely.
        var state = new SessionState();
        state.PingAt(50, 60);

        var restored = new SessionState();
        restored.Restore(state.Capture());

        Assert.False(restored.Ping.IsSet);
    }
}
