using System.Collections.Generic;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Core.Session;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Sessions;

/// <summary>
/// Verifies that everything the players can see survives a capture/restore round trip — the
/// promise that an app restart does not cost an evening's reveals — and that a restore is defensive
/// about the document it is handed.
/// </summary>
public sealed class RevealSnapshotTests
{
    // Expectations hoisted out of the assertions themselves (CA1861), as elsewhere in this suite.
    private static readonly string[] TwoRegions = { "region-entrywell", "region-pillars" };
    private static readonly string[] OneRegion = { "region-entrywell" };
    private static readonly string[] FreshRegion = { "fresh-region" };
    private static readonly (int Col, int Row)[] TwoCells = { (4, 9), (5, 9) };

    [Fact]
    public void Everything_revealed_survives_a_capture_and_restore()
    {
        var state = new SessionState();
        state.SetCurrentMap("level-1-undercroft");
        state.MarkSeeded("level-1-undercroft");
        state.ToggleRegion("region-entrywell");
        state.ToggleRegion("region-pillars");
        state.ToggleFeature("trap-pit-1");
        state.SetDoorOpen("door-7", open: true);
        state.PaintCells(TwoCells);
        state.SetPlayerViewport(new MapBounds(100, 200, 1_100, 762.5));
        state.SetPlayerAspect(16.0 / 10.0);

        RevealSnapshot stored = state.Capture();
        var restored = new SessionState();
        restored.Restore(stored);

        Assert.Equal("level-1-undercroft", restored.CurrentMapId);
        Assert.Equal("level-1-undercroft", restored.SeededMapId);
        Assert.Equal(TwoRegions, restored.RevealedRegionIds().OrderBy(id => id, StringComparer.Ordinal));
        Assert.Equal("trap-pit-1", Assert.Single(restored.RevealedFeatureIds()));
        Assert.Equal("door-7", Assert.Single(restored.OpenDoorIds()));
        Assert.Equal(TwoCells, restored.FogCells().OrderBy(cell => cell.Col).ThenBy(cell => cell.Row));
        Assert.Equal(new MapBounds(100, 200, 1_100, 762.5), restored.PlayerViewport);
        Assert.Equal(16.0 / 10.0, restored.PlayerAspect, 6);
    }

    [Fact]
    public void Restoring_replaces_what_is_held_rather_than_merging_into_it()
    {
        var state = new SessionState();
        state.SetCurrentMap("level-1-undercroft");
        state.ToggleRegion("stale-region");
        state.PaintCell(1, 1, erase: false);
        state.SetDoorOpen("stale-door", open: true);

        state.Restore(new RevealSnapshot
        {
            CurrentMapId = "level-2-arcane",
            RevealedRegionIds = FreshRegion,
        });

        Assert.Equal("level-2-arcane", state.CurrentMapId);
        Assert.Equal("fresh-region", Assert.Single(state.RevealedRegionIds()));
        Assert.Empty(state.FogCells());
        Assert.Empty(state.OpenDoorIds());
    }

    [Fact]
    public void Restoring_raises_changed_once_so_both_views_re_render()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.Restore(new RevealSnapshot { CurrentMapId = "level-1-undercroft" });

        Assert.Equal(1, changes);
    }

    [Fact]
    public void A_null_snapshot_is_ignored()
    {
        var state = new SessionState();
        state.ToggleRegion("region-entrywell");
        int changes = 0;
        state.Changed += () => changes++;

        state.Restore(null);

        Assert.Equal("region-entrywell", Assert.Single(state.RevealedRegionIds()));
        Assert.Equal(0, changes);
    }

    [Fact]
    public void A_restore_drops_the_clear_reveals_undo_rather_than_offering_last_weeks_clear()
    {
        var state = new SessionState();
        state.ToggleRegion("region-entrywell");
        state.ClearReveals();
        Assert.True(state.CanUndoClearReveals);

        state.Restore(state.Capture());

        Assert.False(state.CanUndoClearReveals);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_corrupt_stored_aspect_falls_back_instead_of_collapsing_the_viewport(double aspect)
    {
        var state = new SessionState();
        double fallback = state.PlayerAspect;

        state.Restore(new RevealSnapshot { PlayerAspect = aspect });

        Assert.Equal(fallback, state.PlayerAspect, 6);
    }

    [Fact]
    public void A_stored_aspect_is_clamped_the_same_way_a_reported_one_is()
    {
        var state = new SessionState();

        state.Restore(new RevealSnapshot { PlayerAspect = 400 });

        Assert.Equal(5.0, state.PlayerAspect, 6);
    }

    [Fact]
    public void A_restored_map_is_not_re_seeded_by_the_next_circuit()
    {
        // The bug this guards: seeding used to be tracked per DM circuit, so a refresh (and, once the
        // table was persisted, a restart) re-seeded the map — resetting the projector framing and closing every door the DM
        // had opened. The flag travels with the table now.
        var state = new SessionState();
        state.SetCurrentMap("level-1-undercroft");
        state.MarkSeeded("level-1-undercroft");
        state.SetDoorOpen("door-7", open: true);

        var restored = new SessionState();
        restored.Restore(state.Capture());

        Assert.Equal("level-1-undercroft", restored.SeededMapId);
        Assert.Equal("door-7", Assert.Single(restored.OpenDoorIds()));
    }

    [Fact]
    public void Marking_a_map_seeded_reports_a_change_only_when_it_is_new()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.MarkSeeded("level-1-undercroft");
        state.MarkSeeded("level-1-undercroft");

        Assert.Equal(1, changes);
    }

    [Fact]
    public void Restoring_the_same_map_keeps_its_reveals_when_the_dm_view_points_at_it_again()
    {
        // SetCurrentMap clears reveals when the map changes, and the first DM circuit after a restart
        // calls it with the map it loads. Restoring CurrentMapId is what keeps that call a no-op.
        var state = new SessionState();
        state.SetCurrentMap("level-1-undercroft");
        state.ToggleRegion("region-entrywell");

        var restored = new SessionState();
        restored.Restore(state.Capture());
        restored.SetCurrentMap("level-1-undercroft");

        Assert.Equal(OneRegion, restored.RevealedRegionIds());
    }
}
