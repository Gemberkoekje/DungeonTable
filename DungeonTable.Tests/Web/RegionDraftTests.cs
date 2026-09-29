using DungeonTable.Core.Maps;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The Room Editor's polygon tool, for a room no rectangle fits: corners go down one click at a
/// time, and clicking the first corner again, or the last one again (a double-click), closes the
/// outline once it encloses something.
/// </summary>
public sealed class RegionDraftTests
{
    private const double Tolerance = 3;

    private static readonly MapPoint A = new MapPoint(0, 0);
    private static readonly MapPoint B = new MapPoint(40, 0);
    private static readonly MapPoint C = new MapPoint(40, 30);

    [Fact]
    public void Each_click_becomes_the_next_corner()
    {
        var draft = new RegionDraft();

        Assert.Equal(RegionDraftStep.Added, draft.Add(A, Tolerance));
        Assert.Equal(RegionDraftStep.Added, draft.Add(B, Tolerance));
        Assert.Equal(RegionDraftStep.Added, draft.Add(C, Tolerance));

        Assert.Equal(new[] { A, B, C }, draft.Corners);
        Assert.True(draft.CanClose);
    }

    [Fact]
    public void Clicking_the_first_corner_again_closes_the_outline()
    {
        RegionDraft draft = Triangle();

        Assert.Equal(RegionDraftStep.Closed, draft.Add(new MapPoint(1, -1), Tolerance));
        Assert.Equal(new[] { A, B, C }, draft.Close());
        Assert.True(draft.IsEmpty);
    }

    [Fact]
    public void Clicking_the_last_corner_again_closes_it_which_is_what_a_double_click_does()
    {
        RegionDraft draft = Triangle();

        Assert.Equal(RegionDraftStep.Closed, draft.Add(new MapPoint(41, 31), Tolerance));
        Assert.Equal(3, draft.Corners.Count);
    }

    [Fact]
    public void A_repeated_corner_is_ignored_until_the_outline_encloses_something()
    {
        var draft = new RegionDraft();
        draft.Add(A, Tolerance);
        draft.Add(B, Tolerance);

        // Two corners enclose nothing: a double-click there must neither finish nor add a corner.
        Assert.Equal(RegionDraftStep.Ignored, draft.Add(B, Tolerance));
        Assert.Equal(RegionDraftStep.Ignored, draft.Add(A, Tolerance));
        Assert.Equal(2, draft.Corners.Count);
        Assert.False(draft.CanClose);
    }

    [Fact]
    public void Corners_on_one_line_enclose_nothing_and_cannot_close()
    {
        var draft = new RegionDraft();
        draft.Add(A, Tolerance);
        draft.Add(B, Tolerance);
        draft.Add(new MapPoint(80, 0), Tolerance);

        Assert.False(draft.CanClose);
        Assert.Equal(RegionDraftStep.Ignored, draft.Add(A, Tolerance));
        Assert.Empty(draft.Close());
        Assert.Equal(3, draft.Corners.Count);
    }

    [Fact]
    public void A_click_just_inside_the_tolerance_counts_as_the_corner()
    {
        RegionDraft draft = Triangle();

        Assert.Equal(RegionDraftStep.Closed, draft.Add(new MapPoint(0, 2.9), Tolerance));
    }

    [Fact]
    public void A_click_just_outside_the_tolerance_is_a_new_corner()
    {
        RegionDraft draft = Triangle();

        Assert.Equal(RegionDraftStep.Added, draft.Add(new MapPoint(0, 3.1), Tolerance));
        Assert.Equal(4, draft.Corners.Count);
    }

    [Fact]
    public void A_click_that_is_not_a_point_changes_nothing()
    {
        RegionDraft draft = Triangle();

        Assert.Equal(RegionDraftStep.Ignored, draft.Add(new MapPoint(double.NaN, 5), Tolerance));
        Assert.Equal(RegionDraftStep.Ignored, draft.Add(new MapPoint(5, double.PositiveInfinity), Tolerance));
        Assert.Equal(3, draft.Corners.Count);
    }

    [Fact]
    public void Undo_takes_back_the_last_corner_and_clear_discards_the_outline()
    {
        RegionDraft draft = Triangle();

        draft.Undo();
        Assert.Equal(new[] { A, B }, draft.Corners);
        Assert.False(draft.CanClose);

        draft.Clear();
        Assert.True(draft.IsEmpty);

        draft.Undo();
        Assert.True(draft.IsEmpty);
    }

    [Fact]
    public void An_outline_drawn_clockwise_or_anticlockwise_closes_the_same()
    {
        var anticlockwise = new RegionDraft();
        anticlockwise.Add(C, Tolerance);
        anticlockwise.Add(B, Tolerance);
        anticlockwise.Add(A, Tolerance);

        Assert.True(anticlockwise.CanClose);
        Assert.Equal(new[] { C, B, A }, anticlockwise.Close());
    }

    private static RegionDraft Triangle()
    {
        var draft = new RegionDraft();
        draft.Add(A, Tolerance);
        draft.Add(B, Tolerance);
        draft.Add(C, Tolerance);
        return draft;
    }
}
