using DungeonTable.Core.Art;
using DungeonTable.Core.Session;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Art;

/// <summary>
/// Verifies the projector-art half of <see cref="SessionState"/>: staging shows, hiding does not
/// clear, the tray steps and wraps, and the whole lot survives a capture/restore round trip beside
/// the reveals it lives with.
/// </summary>
public sealed class ShownArtStateTests
{
    private static readonly string[] Tray = { "mm-vampire-spawn", "mm-bandit-captain", "mm-bugbear" };

    [Fact]
    public void Showing_a_picture_stages_it_and_puts_it_on_the_projector()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.ShowArt("mm-vampire-spawn");

        Assert.Equal("mm-vampire-spawn", state.ShownArtImageId);
        Assert.True(state.ShownArtVisible);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Hiding_is_not_clearing()
    {
        // The whole point of the toggle: the DM drops the picture for a beat and brings the same
        // one straight back, without going to find it again.
        var state = new SessionState();
        state.ShowArt("mm-vampire-spawn");
        state.AddToTray("mm-vampire-spawn");

        state.HideArt();

        Assert.False(state.ShownArtVisible);
        Assert.Equal("mm-vampire-spawn", state.ShownArtImageId);
        Assert.Equal("mm-vampire-spawn", Assert.Single(state.TrayImageIds()));

        state.ShowStagedArt();
        Assert.True(state.ShownArtVisible);
    }

    [Fact]
    public void Showing_a_blank_id_clears_the_selection_rather_than_showing_nothing_framed()
    {
        var state = new SessionState();
        state.ShowArt("mm-vampire-spawn");

        state.ShowArt(string.Empty);

        Assert.Equal(string.Empty, state.ShownArtImageId);
        Assert.False(state.ShownArtVisible);
    }

    [Fact]
    public void Showing_the_staged_picture_does_nothing_when_none_is_staged()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.ShowStagedArt();

        Assert.False(state.ShownArtVisible);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void The_tray_holds_each_picture_once_and_in_the_order_it_was_added()
    {
        var state = new SessionState();
        foreach (string id in Tray)
        {
            state.AddToTray(id);
        }

        state.AddToTray("mm-vampire-spawn");
        state.AddToTray(string.Empty);

        Assert.Equal(Tray, state.TrayImageIds());
    }

    [Fact]
    public void Stepping_the_tray_wraps_at_both_ends_and_shows_what_it_lands_on()
    {
        var state = new SessionState();
        foreach (string id in Tray)
        {
            state.AddToTray(id);
        }

        state.ShowArt("mm-bandit-captain");
        state.HideArt();

        state.StepTray(forward: true);
        Assert.Equal("mm-bugbear", state.ShownArtImageId);
        Assert.True(state.ShownArtVisible);

        state.StepTray(forward: true);
        Assert.Equal("mm-vampire-spawn", state.ShownArtImageId);

        state.StepTray(forward: false);
        Assert.Equal("mm-bugbear", state.ShownArtImageId);
    }

    [Fact]
    public void Stepping_from_a_picture_that_is_not_in_the_tray_starts_at_an_end()
    {
        var state = new SessionState();
        foreach (string id in Tray)
        {
            state.AddToTray(id);
        }

        state.ShowArt("mm-something-else");
        state.StepTray(forward: true);
        Assert.Equal("mm-vampire-spawn", state.ShownArtImageId);

        state.ShowArt("mm-something-else");
        state.StepTray(forward: false);
        Assert.Equal("mm-bugbear", state.ShownArtImageId);
    }

    [Fact]
    public void Stepping_an_empty_tray_does_nothing()
    {
        var state = new SessionState();
        state.ShowArt("mm-vampire-spawn");
        int changes = 0;
        state.Changed += () => changes++;

        state.StepTray(forward: true);

        Assert.Equal("mm-vampire-spawn", state.ShownArtImageId);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Dropping_from_the_tray_leaves_the_projector_alone()
    {
        // Tidying the tray mid-scene must not blank the beamer under the DM's hands.
        var state = new SessionState();
        state.AddToTray("mm-vampire-spawn");
        state.ShowArt("mm-vampire-spawn");

        state.RemoveFromTray("mm-vampire-spawn");

        Assert.Empty(state.TrayImageIds());
        Assert.True(state.ShownArtVisible);
        Assert.Equal("mm-vampire-spawn", state.ShownArtImageId);
    }

    [Fact]
    public void Clearing_the_tray_leaves_the_projector_alone_too()
    {
        var state = new SessionState();
        foreach (string id in Tray)
        {
            state.AddToTray(id);
        }

        state.ShowArt("mm-bugbear");

        state.ClearTray();

        Assert.Empty(state.TrayImageIds());
        Assert.True(state.ShownArtVisible);
    }

    [Fact]
    public void The_size_defaults_to_medium_and_an_unset_size_is_read_as_medium()
    {
        var state = new SessionState();
        Assert.Equal(ArtSize.Medium, state.ShownArtSize);

        state.SetArtSize(ArtSize.Full);
        Assert.Equal(ArtSize.Full, state.ShownArtSize);

        state.SetArtSize(ArtSize.None);
        Assert.Equal(ArtSize.Medium, state.ShownArtSize);
    }

    [Fact]
    public void The_staged_picture_the_tray_and_the_size_survive_a_capture_and_restore()
    {
        var state = new SessionState();
        foreach (string id in Tray)
        {
            state.AddToTray(id);
        }

        state.ShowArt("mm-bandit-captain");
        state.SetArtSize(ArtSize.Large);

        RevealSnapshot stored = state.Capture();
        var restored = new SessionState();
        restored.Restore(stored);

        Assert.Equal("mm-bandit-captain", restored.ShownArtImageId);
        Assert.True(restored.ShownArtVisible);
        Assert.Equal(ArtSize.Large, restored.ShownArtSize);
        Assert.Equal(Tray, restored.TrayImageIds());
    }

    [Fact]
    public void A_document_written_before_the_art_existed_restores_the_table_without_one()
    {
        // The other half of "no CurrentVersion bump": an older document has no ShownArt at all.
        var state = new SessionState();
        state.ShowArt("mm-vampire-spawn");

        state.Restore(new RevealSnapshot { CurrentMapId = "level-1-undercroft", ShownArt = null });

        Assert.Equal("level-1-undercroft", state.CurrentMapId);
        Assert.Equal(string.Empty, state.ShownArtImageId);
        Assert.False(state.ShownArtVisible);
        Assert.Equal(ArtSize.Medium, state.ShownArtSize);
        Assert.Empty(state.TrayImageIds());
    }

    [Fact]
    public void A_stored_visible_flag_with_no_picture_restores_as_hidden()
    {
        // A hand-edited or half-written document must not leave the projector claiming to show a
        // picture it cannot name — the overlay would render an empty frame over the map.
        var state = new SessionState();

        state.Restore(new RevealSnapshot
        {
            ShownArt = new ShownArtSnapshot { ImageId = string.Empty, Visible = true },
        });

        Assert.False(state.ShownArtVisible);
    }
}
