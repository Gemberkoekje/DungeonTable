using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Infrastructure.Maps;
using DungeonTable.Tests.Web;
using DungeonTable.Web.Components.Shared;
using DungeonTable.Web.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// The DM map is drawn in three layers (the map, the fog, the editor overlay) so that a reveal sends
/// only the fog. Together they draw exactly what the single SVG did, and the map layer's key changes
/// exactly when the map layer would.
/// </summary>
public sealed class VectorMapLayersTests
{
    private static readonly string[] ARoomAndAMarker = { "f-a-trap", "r-a-room" };

    private static VectorMap Map => SampleMap.Map;

    private static string[] SecretDoors => SampleMap.SecretDoors;

    private static string[] DoubleDoors => SampleMap.DoubleDoors;

    private static string[] Concealed => SampleMap.Concealed;

    private static string[] PlainDoors => SampleMap.PlainDoors;

    [Fact]
    public async Task The_canvas_draws_exactly_what_the_single_svg_did()
    {
        // Everything that reaches a layer: a revealed room and painted cells under the fog, an open door,
        // a discovered secret door, a concealed object, and the overlay's regions with one selected.
        Region room = SampleMap.Notes.Regions[0];
        var fog = new FogOverlay
        {
            Enabled = true,
            FogColour = "#1b2436",
            FogOpacity = 0.5,
            RevealedPolygons = new[] { room.Polygon },
            RevealedCells = new[] { GridCells.CellBounds(Map.Grid, 3, 4), GridCells.CellBounds(Map.Grid, 4, 4), GridCells.CellBounds(Map.Grid, 9, 2) },
            UnitSize = Map.Grid.CellSizePx,
        };
        var overlay = new EditorOverlay
        {
            Regions = SampleMap.Notes.Regions,
            Markers = SampleMap.Notes.Features,
            SelectedRegionId = room.RegionId,
            UnitSize = Map.Grid.CellSizePx,
        };
        string[] open = PlainDoors.Take(1).ToArray();
        string[] revealed = SecretDoors.Take(1).ToArray();

        string canvas = await RenderCanvas(new Dictionary<string, object>
        {
            [nameof(VectorMapCanvas.Map)] = Map,
            [nameof(VectorMapCanvas.ShowGrid)] = true,
            [nameof(VectorMapCanvas.SecretDoorIds)] = SecretDoors,
            [nameof(VectorMapCanvas.MarkSecretDoors)] = true,
            [nameof(VectorMapCanvas.RevealedFeatureIds)] = revealed,
            [nameof(VectorMapCanvas.ConcealedObjectIds)] = Concealed,
            [nameof(VectorMapCanvas.OpenDoorIds)] = open,
            [nameof(VectorMapCanvas.DoubleDoorIds)] = DoubleDoors,
            [nameof(VectorMapCanvas.Fog)] = fog,
            [nameof(VectorMapCanvas.Overlay)] = overlay,
        });
        string single = VectorMapSvgRenderer.Render(Map, new VectorMapRenderOptions
        {
            ShowGrid = true,
            Overlay = overlay,
            Doors = new DoorRenderState { SecretDoorIds = SecretDoors, DiscoveredDoorIds = revealed, OpenDoorIds = open, DoubleDoorIds = DoubleDoors },
            MarkSecretDoors = true,
            Conceal = new ConcealState { ConcealedIds = Concealed, RevealedIds = revealed },
            Fog = fog,
        });

        Assert.Contains("<g class=\"dt-live\"></g></svg>", canvas, StringComparison.Ordinal);
        string svg = canvas.Substring(canvas.IndexOf("<svg", StringComparison.Ordinal));
        svg = svg.Substring(0, svg.LastIndexOf("</svg>", StringComparison.Ordinal) + "</svg>".Length);
        Assert.Equal(single, svg.Replace("<g class=\"dt-live\"></g>", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_door_opened_between_renders_shows_open_on_the_next_render()
    {
        // The canvas keeps the map layer it rendered until its key changes; opening a door must change it.
        var open = new List<string>();
        string[] door = PlainDoors.Take(1).ToArray();
        RenderAgainHost host = null;
        RenderFragment content = builder =>
        {
            builder.OpenComponent<VectorMapCanvas>(0);
            builder.AddComponentParameter(1, nameof(VectorMapCanvas.Map), Map);
            builder.AddComponentParameter(2, nameof(VectorMapCanvas.OpenDoorIds), open.ToArray());
            builder.CloseComponent();
        };

        await using ServiceProvider services = new ServiceCollection().AddSingleton<IJSRuntime, NoJavaScript>().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        (string first, string second) = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = new Dictionary<string, object>
            {
                [nameof(RenderAgainHost.ChildContent)] = content,
                [nameof(RenderAgainHost.OnReady)] = (Action<RenderAgainHost>)(h => host = h),
            };
            HtmlRootComponent output = await renderer.RenderComponentAsync<RenderAgainHost>(ParameterView.FromDictionary(parameters));
            string html = output.ToHtmlString();
            open.AddRange(door);
            await host.RenderAgainAsync();
            return (html, output.ToHtmlString());
        });

        string expected = VectorMapSvgRenderer.RenderMapLayer(Map, new VectorMapRenderOptions { Doors = new DoorRenderState { OpenDoorIds = door } });
        Assert.DoesNotContain(expected, first, StringComparison.Ordinal);
        Assert.Contains(expected, second, StringComparison.Ordinal);
    }

    [Fact]
    public void Revealing_a_room_or_a_marker_leaves_the_map_layer_alone()
    {
        VectorMapRenderOptions before = Options(revealed: Array.Empty<string>());
        VectorMapRenderOptions after = Options(revealed: ARoomAndAMarker);

        Assert.Equal(VectorMapSvgRenderer.MapLayerKey(before), VectorMapSvgRenderer.MapLayerKey(after));
        Assert.Equal(VectorMapSvgRenderer.RenderMapLayer(Map, before), VectorMapSvgRenderer.RenderMapLayer(Map, after));
    }

    [Fact]
    public void The_map_layer_key_does_not_depend_on_the_order_of_the_ids()
    {
        string[] doors = Map.Doors.Select(d => d.Id).ToArray();

        Assert.Equal(
            VectorMapSvgRenderer.MapLayerKey(Options(open: doors)),
            VectorMapSvgRenderer.MapLayerKey(Options(open: doors.Reverse().ToArray())));
    }

    /// <summary>Each change to what the map layer draws, as a pair of options before and after it.</summary>
    public static TheoryData<string> Changes => new TheoryData<string>
    {
        "open a door", "discover a secret door", "show a concealed object", "hide the grid", "make a door double", "unmark secret doors",
    };

    [Theory]
    [MemberData(nameof(Changes))]
    public void A_change_to_what_the_map_layer_draws_changes_its_key(string change)
    {
        VectorMapRenderOptions before = Options();
        VectorMapRenderOptions after = change switch
        {
            "open a door" => Options(open: PlainDoors.Take(1).ToArray()),
            "discover a secret door" => Options(revealed: SecretDoors),
            "show a concealed object" => Options(revealed: Concealed.Take(1).ToArray()),
            "hide the grid" => Options(showGrid: false),
            "make a door double" => Options(doubles: PlainDoors.Except(DoubleDoors).Take(1).Concat(DoubleDoors).ToArray()),
            _ => Options(markSecret: false),
        };

        Assert.NotEqual(VectorMapSvgRenderer.RenderMapLayer(Map, before), VectorMapSvgRenderer.RenderMapLayer(Map, after));
        Assert.NotEqual(VectorMapSvgRenderer.MapLayerKey(before), VectorMapSvgRenderer.MapLayerKey(after));
    }

    [Fact]
    public void Touching_cells_in_a_row_merge_into_one_run_and_the_rest_stay_apart()
    {
        // Two rows: three touching cells and a lone one after a gap, then one cell below, plus a duplicate
        // and a cell with a non-finite corner that must be dropped.
        var cells = new[]
        {
            new MapBounds(20, 0, 30, 10), new MapBounds(0, 0, 10, 10), new MapBounds(10, 0, 20, 10),
            new MapBounds(50, 0, 60, 10), new MapBounds(0, 10, 10, 20), new MapBounds(10, 0, 20, 10),
            new MapBounds(double.NaN, 0, 10, 10),
        };

        string d = VectorMapSvgRenderer.CellRunsPathData(cells);

        Assert.Equal("M0 0H30V10H0ZM50 0H60V10H50ZM0 10H10V20H0Z", d);
    }

    [Fact]
    public void Cells_whose_shared_edge_differs_in_the_last_bit_still_merge()
    {
        // x + size for one cell and the next cell's own x can differ in the last bit of a double: here the
        // next cell starts a hair after the first one ends (0.1 + 0.2 is 0.30000000000000004).
        double start = 0.1 + 0.2;
        var cells = new[] { new MapBounds(0, 0, 0.3, 1), new MapBounds(start, 0, 0.6, 1) };

        Assert.Equal("M0 0H0.6V1H0Z", VectorMapSvgRenderer.CellRunsPathData(cells));
    }

    private static VectorMapRenderOptions Options(
        string[] revealed = null, string[] open = null, string[] doubles = null, bool showGrid = true, bool markSecret = true)
    {
        string[] shown = revealed ?? Array.Empty<string>();
        return new VectorMapRenderOptions
        {
            ShowGrid = showGrid,
            MarkSecretDoors = markSecret,
            Doors = new DoorRenderState
            {
                SecretDoorIds = SecretDoors,
                DiscoveredDoorIds = shown,
                OpenDoorIds = open ?? Array.Empty<string>(),
                DoubleDoorIds = doubles ?? DoubleDoors,
            },
            Conceal = new ConcealState { ConcealedIds = Concealed, RevealedIds = shown },
        };
    }

    internal static async Task<string> RenderCanvas(Dictionary<string, object> parameters)
    {
        await using ServiceProvider services = new ServiceCollection()
            .AddSingleton<IJSRuntime, NoJavaScript>()
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent output = await renderer.RenderComponentAsync<VectorMapCanvas>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }

    // A static render never reaches the canvas's JavaScript; the canvas only needs one to be injected.
    internal sealed class NoJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object[] args) => ValueTask.FromResult(default(TValue));

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object[] args) =>
            ValueTask.FromResult(default(TValue));
    }
}
