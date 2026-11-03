using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Infrastructure.Maps;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// How fast the fog lifter is and how much each reveal sends, as numbers. It is explicit, so the build
/// never runs it: timings are no use as a pass or fail on a shared CI machine. Run it on purpose, in a
/// Release build, which is what the container runs:
/// <code>dotnet test DungeonTable.Tests -c Release --filter "FullyQualifiedName~LiftBenchmark" -- xUnit.Explicit=only</code>
/// It runs on the sample pack's map; set <c>DUNGEONTABLE_BENCH_MAP</c> to a <c>.ds</c> file to run it on
/// another map. See <c>docs/architecture.md</c>, "The fog lifter".
/// </summary>
public sealed class LiftBenchmark
{
    private static readonly string[] None = Array.Empty<string>();

    private static readonly int[] Ranges = { 30, 60, 120 };

    [Fact(Explicit = true)]
    public async Task Lift_times_and_layer_sizes()
    {
        string path = Environment.GetEnvironmentVariable("DUNGEONTABLE_BENCH_MAP") ?? string.Empty;
        if (path.Length == 0)
        {
            path = SamplePack.MapFile;
        }

        var read = await new DungeonScrawlMapReader().ReadFileAsync(path, CancellationToken.None);
        Assert.True(read.IsValid, $"Could not read {path}.");
        VectorMap map = read.Value;
        Grid g = map.Grid;
        Report($"{Path.GetFileName(path)}: grid {g.Cols}x{g.Rows}, {map.Doors.Count} doors");

        // Twelve spots on the floor, the same ones every run.
        var floor = new List<(int Col, int Row)>();
        for (int col = 0; col < g.Cols; col++)
        {
            for (int row = 0; row < g.Rows; row++)
            {
                if (VisionIndex.For(map).FloorMask(col, row) != 0)
                {
                    floor.Add((col, row));
                }
            }
        }

        var spots = Enumerable.Range(0, 12).Select(i => floor[(int)((long)i * 7919 % floor.Count)]).ToList();
        var seen = new HashSet<(int, int)>();
        foreach (int feet in Ranges)
        {
            var times = new List<double>();
            int cells = 0;
            foreach ((int col, int row) in spots)
            {
                MapBounds b = GridCells.CellBounds(g, col, row);
                var watch = Stopwatch.StartNew();
                IReadOnlyList<(int Col, int Row)> found = VisionReveal.Find(map, (b.MinX + b.MaxX) / 2, (b.MinY + b.MaxY) / 2, feet, None, None);
                times.Add(watch.Elapsed.TotalMilliseconds);
                cells += found.Count;
                seen.UnionWith(found);
            }

            times.Sort();
            Report($"lift {feet,3} ft: median {times[times.Count / 2],6:F1} ms, slowest {times[^1],6:F1} ms, {cells / spots.Count} cells on average");
        }

        // What one more reveal sends once this much is revealed, on each view.
        var revealed = seen.Select(c => GridCells.CellBounds(g, c.Item1, c.Item2)).ToList();
        var fog = new FogOverlay { Enabled = true, FogColour = "#1b2436", FogOpacity = 0.5, RevealedCells = revealed, UnitSize = g.CellSizePx };
        var dm = new VectorMapRenderOptions { ShowGrid = false, Fog = fog };
        Report($"DM map with {revealed.Count} cells revealed: map layer {Size(() => VectorMapSvgRenderer.RenderMapLayer(map, dm))} once, "
            + $"fog layer {Size(() => VectorMapSvgRenderer.RenderFogLayer(map, dm))} per reveal");

        IReadOnlyList<IReadOnlyList<MapPoint>> regions = Array.Empty<IReadOnlyList<MapPoint>>();
        IReadOnlyList<FeatureMarker> markers = Array.Empty<FeatureMarker>();
        (string Reveal, string Objects) layers = (string.Empty, string.Empty);
        string layersTime = Time(() => layers = VectorMapSvgRenderer.RenderRevealedLayers(map, regions, revealed, markers, DoorRenderState.None, ConcealState.None));
        Report($"Projector: map layer {Size(() => VectorMapSvgRenderer.RenderRevealedMapLayer(map))} once, "
            + $"reveal layer {layers.Reveal.Length / 1024} KB per reveal and object layer {layers.Objects.Length / 1024} KB per door change ({layersTime})");
    }

    private static string Size(Func<string> render)
    {
        string text = string.Empty;
        string time = Time(() => text = render());
        return $"{text.Length / 1024} KB ({time})";
    }

    // The second of two runs, so the first pays for loading and compiling.
    private static string Time(Action render)
    {
        render();
        var watch = Stopwatch.StartNew();
        render();
        return $"{watch.Elapsed.TotalMilliseconds:F0} ms";
    }

    private static void Report(string line) => TestContext.Current.TestOutputHelper?.WriteLine(line);
}
