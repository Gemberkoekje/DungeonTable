using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Art;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Core.Session;
using DungeonTable.Web.Components.Player;
using DungeonTable.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The projector for a table the players sit around: the map turned a quarter at a time, a picture
/// turned and flipped on its own, and a size of view in squares remembered on one map and used on the
/// next, so a square on the table stays the size of a miniature's base.
/// </summary>
public sealed class ProjectorTurnTests
{
    private static readonly MapBounds Map = new MapBounds(0, 0, 1000, 800);

    // ---- The picture over the map -----------------------------------------------------------

    [Fact]
    public void A_picture_turns_a_quarter_at_a_time_and_flips_and_each_picture_keeps_its_own()
    {
        var state = new SessionState();
        int changes = 0;
        state.Changed += () => changes++;

        state.TurnArt("portrait");
        state.TurnArt("portrait");
        state.MirrorArt("portrait");
        state.TurnArt("map-handout");

        Assert.Equal(new ArtOrientation(2, true), state.ArtOrientationOf("portrait"));
        Assert.Equal(new ArtOrientation(1, false), state.ArtOrientationOf("map-handout"));
        Assert.Equal(ArtOrientation.Upright, state.ArtOrientationOf("other"));
        Assert.Equal(4, changes);

        state.TurnArt(" ");
        Assert.Equal(4, changes);
    }

    [Fact]
    public void A_picture_turned_all_the_way_round_and_flipped_back_is_upright_and_not_kept()
    {
        var state = new SessionState();
        state.MirrorArt("portrait");
        for (int i = 0; i < 4; i++)
        {
            state.TurnArt("portrait");
        }

        state.MirrorArt("portrait");

        Assert.True(state.ArtOrientationOf("portrait").IsUpright);
        Assert.Empty(state.Capture().ShownArt.Orientations);
    }

    [Fact]
    public void How_each_picture_is_turned_survives_a_capture_and_restore_once_each()
    {
        var state = new SessionState();
        state.TurnArt("portrait");
        state.MirrorArt("map-handout");

        RevealSnapshot saved = state.Capture();
        Assert.Equal(2, saved.ShownArt.Orientations.Count);

        // A hand-edited document: the same picture twice, a turn past three, and an entry of nothing.
        saved.ShownArt.Orientations = new[]
        {
            new ArtOrientationSnapshot { ImageId = "portrait", QuarterTurns = 5 },
            new ArtOrientationSnapshot { ImageId = "portrait", QuarterTurns = 2 },
            new ArtOrientationSnapshot { ImageId = "upright", QuarterTurns = 4 },
            new ArtOrientationSnapshot { ImageId = string.Empty, Mirrored = true },
            new ArtOrientationSnapshot { ImageId = "backwards", QuarterTurns = -1 },
        };

        var restored = new SessionState();
        restored.Restore(saved);

        Assert.Equal(new ArtOrientation(1, false), restored.ArtOrientationOf("portrait"));
        Assert.Equal(new ArtOrientation(3, false), restored.ArtOrientationOf("backwards"));
        Assert.Equal(2, restored.Capture().ShownArt.Orientations.Count);
    }

    [Fact]
    public async Task A_picture_on_its_side_is_turned_in_its_frame_and_at_full_size_fills_the_screen_the_other_way()
    {
        string upright = await RenderArt(ArtSize.Full, ArtOrientation.Upright);
        string onItsSide = await RenderArt(ArtSize.Full, new ArtOrientation(1, true));
        string medium = await RenderArt(ArtSize.Medium, new ArtOrientation(2, false));

        // A 600 by 900 portrait: upright, as high as the screen allows; on its side, as wide.
        Assert.Contains("width: min(96vw, calc(94vh * 0.6667))", upright, StringComparison.Ordinal);
        Assert.DoesNotContain("on-side", upright, StringComparison.Ordinal);
        Assert.DoesNotContain("rotate(", upright, StringComparison.Ordinal);

        Assert.Contains("player-art art-full on-side", onItsSide, StringComparison.Ordinal);
        Assert.Contains("transform: rotate(90deg)", onItsSide, StringComparison.Ordinal);
        Assert.Contains("class=\"mirrored\"", onItsSide, StringComparison.Ordinal);
        Assert.Contains("width: min(94vh, calc(96vw * 0.6667))", onItsSide, StringComparison.Ordinal);

        // Below full the picture keeps the size limits, the other way round only on its side.
        Assert.Contains("transform: rotate(180deg)", medium, StringComparison.Ordinal);
        Assert.DoesNotContain("on-side", medium, StringComparison.Ordinal);
        Assert.DoesNotContain("min(", medium, StringComparison.Ordinal);
    }

    // ---- The map on the projector ----------------------------------------------------------

    [Fact]
    public void Turning_the_projector_turns_its_view_about_the_middle_and_cuts_framing_the_other_way_up()
    {
        var state = new SessionState();
        state.SetPlayerAspect(16.0 / 9.0);
        state.SetPlayerViewport(new MapBounds(100, 200, 260, 290));

        state.TurnPlayerView();

        Assert.Equal(1, state.PlayerTurns);
        Assert.Equal(new MapBounds(135, 165, 225, 325), state.PlayerViewport);
        Assert.Equal(9.0 / 16.0, state.FramingAspect, 6);

        state.TurnPlayerView();
        Assert.Equal(16.0 / 9.0, state.FramingAspect, 6);
        Assert.Equal(new MapBounds(100, 200, 260, 290), state.PlayerViewport);

        state.TurnPlayerView();
        state.TurnPlayerView();
        Assert.Equal(0, state.PlayerTurns);
    }

    [Fact]
    public void Turning_the_projector_turns_the_view_kept_for_every_other_map_too()
    {
        var state = new SessionState();
        state.SetCurrentMap("level-1");
        state.SetPlayerViewport(new MapBounds(0, 0, 160, 90));
        state.MarkSeeded("level-1");
        state.SetCurrentMap("level-2");
        state.SetPlayerViewport(new MapBounds(0, 0, 320, 180));

        state.TurnPlayerView();
        state.SetCurrentMap("level-1");

        Assert.Equal(new MapBounds(35, -35, 125, 125), state.PlayerViewport);
    }

    [Fact]
    public void The_turn_and_the_remembered_size_survive_a_capture_and_restore()
    {
        var state = new SessionState();
        state.TurnPlayerView();
        state.RememberView(new ViewSize(24, 13.5));
        state.RememberView(new ViewSize(double.NaN, 3));
        state.RememberView(new ViewSize(0, 3));

        RevealSnapshot saved = state.Capture();
        var restored = new SessionState();
        restored.Restore(saved);

        Assert.Equal(1, restored.PlayerTurns);
        Assert.Equal(new ViewSize(24, 13.5), restored.RememberedView);

        saved.PlayerTurns = 7;
        saved.RememberedView = new ViewSizeSnapshot { SquaresAcross = -2, SquaresDown = 3 };
        restored.Restore(saved);
        Assert.Equal(3, restored.PlayerTurns);
        Assert.False(restored.RememberedView.IsSet);
    }

    [Fact]
    public void A_document_written_before_the_turn_restores_upright_with_no_size()
    {
        var restored = new SessionState();
        restored.Restore(new RevealSnapshot { RememberedView = null });

        Assert.Equal(0, restored.PlayerTurns);
        Assert.Equal(ViewSize.None, restored.RememberedView);
    }

    // ---- A size in squares, from one map to the next ---------------------------------------

    [Fact]
    public void A_remembered_size_frames_the_same_squares_across_about_the_middle_of_the_view()
    {
        // 20 squares across a 16:9 screen, on a map of 10-unit squares: 200 by 112.5, about (500, 400).
        MapBounds view = MapFraming.AtSize(new MapBounds(450, 350, 550, 450), new ViewSize(20, 11.25), 10, 16.0 / 9.0, 0, Map);

        Assert.Equal(new MapBounds(400, 343.75, 600, 456.25), view);
    }

    [Fact]
    public void On_its_side_the_squares_across_the_screen_run_down_the_map()
    {
        MapBounds view = MapFraming.AtSize(new MapBounds(450, 350, 550, 450), new ViewSize(20, 11.25), 10, 16.0 / 9.0, 1, Map);

        Assert.Equal(112.5, view.Width, 6);
        Assert.Equal(200, view.Height, 6);
    }

    [Fact]
    public void A_remembered_size_is_moved_onto_the_map_and_never_shrunk_to_fit_it()
    {
        // At the corner: slid in. Larger than the map: centred on it, at the same scale.
        MapBounds corner = MapFraming.AtSize(new MapBounds(0, 0, 10, 10), new ViewSize(20, 11.25), 10, 16.0 / 9.0, 0, Map);
        MapBounds huge = MapFraming.AtSize(new MapBounds(0, 0, 10, 10), new ViewSize(200, 112.5), 10, 16.0 / 9.0, 0, Map);

        Assert.Equal(new MapBounds(0, 0, 200, 112.5), corner);
        Assert.Equal(2000, huge.Width, 6);
        Assert.Equal(-500, huge.MinX, 6);
        Assert.Equal(-162.5, huge.MinY, 6);
    }

    [Fact]
    public void No_size_or_no_square_leaves_the_view_as_it_is()
    {
        var view = new MapBounds(1, 2, 3, 4);

        Assert.Equal(view, MapFraming.AtSize(view, ViewSize.None, 10, 1.5, 0, Map));
        Assert.Equal(view, MapFraming.AtSize(view, new ViewSize(5, 5), 0, 1.5, 0, Map));
    }

    private static async Task<string> RenderArt(ArtSize size, ArtOrientation orientation)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IArtCatalogue>(new OnePicture());
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);

        var parameters = new Dictionary<string, object>
        {
            [nameof(PlayerArtOverlay.ImageId)] = "portrait",
            [nameof(PlayerArtOverlay.Size)] = size,
            [nameof(PlayerArtOverlay.Orientation)] = orientation,
        };

        string html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent output = await renderer.RenderComponentAsync<PlayerArtOverlay>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });

        // The element only: the overlay carries its own style block, which names every class.
        int element = html.IndexOf("<div class=\"player-art", StringComparison.Ordinal);
        Assert.True(element >= 0, "No picture was drawn.");
        return html[element..];
    }

    // A catalogue of one 600 by 900 portrait.
    private sealed class OnePicture : IArtCatalogue
    {
        private static readonly ArtImage Portrait = new ArtImage { Id = "portrait", File = "portrait.webp", Title = "Portrait", Width = 600, Height = 900 };

        // Never raised: the catalogue of one never changes, so nobody needs to be told.
        public event Action Changed
        {
            add => _ = value;
            remove => _ = value;
        }

        public IReadOnlyList<ArtImage> All() => new[] { Portrait };

        public Result<ArtImage> GetImage(string imageId) =>
            string.Equals(imageId, Portrait.Id, StringComparison.Ordinal)
                ? Result.For(Portrait)
                : Result.WithMessages<ArtImage>(ValidationMessage.Error("No such picture.", nameof(imageId)));

        public IReadOnlyList<ArtImage> Search(string query, int limit) => All();

        public IReadOnlyList<ArtImage> ForNode(string nodeId) => Array.Empty<ArtImage>();

        public Result<ArtImage> Upload(string title, byte[] content) =>
            Result.WithMessages<ArtImage>(ValidationMessage.Error("No uploads.", nameof(content)));

        public Result Remove(string imageId) => Result.OK;
    }
}
