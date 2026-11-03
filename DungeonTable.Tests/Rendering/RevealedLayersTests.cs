using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Components.Shared;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// The projector's picture is sent in three layers (the map's geometry once, then the revealed area and
/// the objects on it), and the layers draw what the single SVG draws. "The same" is checked on the
/// markup: every <c>use</c> of the map layer's geometry is replaced by that geometry with the use's
/// attributes, which is what a browser draws for it, and then both documents must define the same
/// things under the same ids and paint the same elements in the same order.
/// </summary>
public sealed class RevealedLayersTests
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    /// <summary>The table at eight moments of an evening, from nothing revealed to doors opened and found.</summary>
    public static TheoryData<int> Moments => new TheoryData<int> { 0, 1, 2, 3, 4, 5, 6, 7 };

    [Theory]
    [MemberData(nameof(Moments))]
    public void The_projectors_layers_draw_what_its_single_svg_draws(int moment)
    {
        Reveal r = At(moment);

        string single = VectorMapSvgRenderer.RenderRevealed(SampleMap.Map, r.View, r.Regions, r.Cells, r.Markers, r.Doors, r.Conceal);
        string layered = Layered(r);

        Assert.Equal(Canonical(single), Canonical(layered));
    }

    [Fact]
    public async Task The_player_canvas_draws_the_three_layers_in_order()
    {
        Reveal r = At(7);
        var fog = new FogOverlay
        {
            Enabled = true,
            BookEdge = true,
            RevealedPolygons = r.Regions,
            RevealedCells = r.Cells,
            RevealedMarkers = r.Markers,
        };

        string canvas = await VectorMapLayersTests.RenderCanvas(new Dictionary<string, object>
        {
            [nameof(VectorMapCanvas.Map)] = SampleMap.Map,
            [nameof(VectorMapCanvas.ViewBox)] = r.View,
            [nameof(VectorMapCanvas.SecretDoorIds)] = r.Doors.SecretDoorIds,
            [nameof(VectorMapCanvas.RevealedFeatureIds)] = r.Doors.DiscoveredDoorIds.Concat(r.Conceal.RevealedIds).ToArray(),
            [nameof(VectorMapCanvas.ConcealedObjectIds)] = r.Conceal.ConcealedIds,
            [nameof(VectorMapCanvas.OpenDoorIds)] = r.Doors.OpenDoorIds,
            [nameof(VectorMapCanvas.DoubleDoorIds)] = r.Doors.DoubleDoorIds,
            [nameof(VectorMapCanvas.Fog)] = fog,
        });

        string svg = canvas.Substring(canvas.IndexOf("<svg", StringComparison.Ordinal));
        svg = svg.Substring(0, svg.LastIndexOf("</svg>", StringComparison.Ordinal) + "</svg>".Length);
        Assert.Equal(Layered(r), svg.Replace("<g class=\"dt-live\"></g>", string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public void The_reveal_layer_draws_the_floor_and_walls_by_reference_and_leaves_the_objects_out()
    {
        // The geometry is most of the weight: it goes once, in the map layer, and the layer a reveal changes
        // only points at it. The doors and objects change with the doors, not with the reveal.
        Reveal r = At(7);
        string mapLayer = VectorMapSvgRenderer.RenderRevealedMapLayer(SampleMap.Map);
        (string reveal, string objects) = VectorMapSvgRenderer.RenderRevealedLayers(SampleMap.Map, r.Regions, r.Cells, r.Markers, r.Doors, r.Conceal);

        string floor = FloorPathData(mapLayer);
        Assert.True(floor.Length > 200, "The sample map's floor should be a long path.");
        Assert.DoesNotContain(floor, reveal, StringComparison.Ordinal);
        Assert.Contains("<use href=\"#dt-p-floor\"", reveal, StringComparison.Ordinal);
        Assert.DoesNotContain("data-object-id", reveal, StringComparison.Ordinal);
        Assert.Contains("<g clip-path=\"url(#dt-drawn)\">", objects, StringComparison.Ordinal);
    }

    [Fact]
    public void Revealing_more_leaves_the_object_layer_as_it_was()
    {
        Reveal before = At(1);
        Assert.NotEmpty(before.Cells);
        var more = new List<MapBounds>(before.Cells);
        more.AddRange(At(2).Cells);

        string objectsBefore = VectorMapSvgRenderer.RenderRevealedLayers(SampleMap.Map, before.Regions, before.Cells, before.Markers, before.Doors, before.Conceal).Objects;
        string objectsAfter = VectorMapSvgRenderer.RenderRevealedLayers(SampleMap.Map, before.Regions, more, before.Markers, before.Doors, before.Conceal).Objects;

        Assert.Equal(objectsBefore, objectsAfter);
    }

    [Fact]
    public void With_nothing_revealed_the_projector_shows_only_the_black_ground()
    {
        Reveal r = At(0);
        (string reveal, string objects) = VectorMapSvgRenderer.RenderRevealedLayers(SampleMap.Map, r.Regions, r.Cells, r.Markers, r.Doors, r.Conceal);

        Assert.Empty(objects);
        Assert.DoesNotContain("<use", reveal, StringComparison.Ordinal);
        Assert.Contains("fill=\"#05060a\"", VectorMapSvgRenderer.RenderRevealedMapLayer(SampleMap.Map), StringComparison.Ordinal);
    }

    private static string Layered(Reveal r)
    {
        (string reveal, string objects) = VectorMapSvgRenderer.RenderRevealedLayers(SampleMap.Map, r.Regions, r.Cells, r.Markers, r.Doors, r.Conceal);
        return "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"" + VectorMapSvgRenderer.RevealedViewBox(SampleMap.Map, r.View)
            + "\" preserveAspectRatio=\"xMidYMid meet\" class=\"vector-map\">"
            + VectorMapSvgRenderer.RenderRevealedMapLayer(SampleMap.Map) + reveal + objects + "</svg>";
    }

    private static string FloorPathData(string mapLayer)
    {
        const string start = "<path id=\"dt-p-floor\" d=\"";
        int at = mapLayer.IndexOf(start, StringComparison.Ordinal) + start.Length;
        return mapLayer.Substring(at, mapLayer.IndexOf('"', at) - at);
    }

    // The reveal at a moment of an evening: what is lifted grows from spots across the map, doors open,
    // rooms are revealed whole, a concealed object is shown, the markers appear, a secret door is found
    // and then opened, and once the projector looks at only part of the map.
    private static Reveal At(int moment)
    {
        VectorMap map = SampleMap.Map;
        Grid g = map.Grid;
        string[] secret = SampleMap.SecretDoors;
        string[] plain = SampleMap.PlainDoors;
        var lifted = new HashSet<(int, int)>();
        var open = new List<string>();
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

        for (int step = 1; step <= moment; step++)
        {
            (int col, int row) = floor[(step * 37) % floor.Count];
            MapBounds spot = GridCells.CellBounds(g, col, row);
            foreach ((int, int) cell in VisionReveal.Find(map, (spot.MinX + spot.MaxX) / 2, (spot.MinY + spot.MaxY) / 2, step % 2 == 0 ? 60 : 30, secret, open))
            {
                lifted.Add(cell);
            }

            open.Add(plain[step % plain.Length]);
        }

        string[] found = moment >= 6 ? secret.Take(1).ToArray() : Array.Empty<string>();
        return new Reveal
        {
            View = moment == 2 ? new MapBounds(map.Bounds.MinX, map.Bounds.MinY, map.Bounds.MinX + (map.Bounds.Width / 2), map.Bounds.MinY + (map.Bounds.Height / 2)) : new MapBounds(),
            Regions = SampleMap.Notes.Regions.Where((_, i) => moment >= 3 && i % 3 == moment % 3).Select(x => x.Polygon).ToList(),
            Cells = lifted.Select(x => GridCells.CellBounds(g, x.Item1, x.Item2)).ToList(),
            Markers = moment >= 5 ? SampleMap.Notes.Features.ToList() : new List<FeatureMarker>(),
            Doors = new DoorRenderState
            {
                SecretDoorIds = secret,
                DiscoveredDoorIds = found,
                OpenDoorIds = open.Concat(moment >= 7 ? found : Array.Empty<string>()).ToArray(),
                DoubleDoorIds = SampleMap.DoubleDoors,
            },
            Conceal = new ConcealState { ConcealedIds = SampleMap.Concealed, RevealedIds = moment >= 4 ? SampleMap.Concealed.Take(1).ToArray() : Array.Empty<string>() },
        };
    }

    // The document as a browser draws it: each use of the map layer's geometry (dt-p-*) replaced by that
    // geometry carrying the use's attributes, then what is defined (by id, in any order) and what is
    // painted (in order), each element written with its attributes sorted.
    private static string Canonical(string svg)
    {
        XElement root = XElement.Parse(svg);
        var defined = root.Elements(Svg + "defs").SelectMany(d => d.Elements()).Where(e => e.Attribute("id") is not null)
            .ToDictionary(e => (string)e.Attribute("id"), StringComparer.Ordinal);

        foreach (XElement use in root.Descendants(Svg + "use").ToList())
        {
            string href = (string)use.Attribute("href") ?? string.Empty;
            if (!href.StartsWith("#dt-p-", StringComparison.Ordinal))
            {
                continue;
            }

            var drawn = new XElement(defined[href.Substring(1)]);
            drawn.Attribute("id").Remove();
            foreach (XAttribute attribute in use.Attributes().Where(a => a.Name != "href"))
            {
                drawn.SetAttributeValue(attribute.Name, attribute.Value);
            }

            use.ReplaceWith(drawn);
        }

        var text = new StringBuilder("DEFINED\n");
        foreach (KeyValuePair<string, XElement> entry in defined.Where(d => !d.Key.StartsWith("dt-p-", StringComparison.Ordinal)).OrderBy(d => d.Key, StringComparer.Ordinal))
        {
            Write(text, entry.Value);
            text.Append('\n');
        }

        text.Append("PAINTED\n");
        foreach (XElement painted in root.Elements().Where(e => e.Name != Svg + "defs"))
        {
            Write(text, painted);
            text.Append('\n');
        }

        return text.ToString();
    }

    private static void Write(StringBuilder text, XElement element)
    {
        text.Append('<').Append(element.Name.LocalName);
        foreach (XAttribute attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration).OrderBy(a => a.Name.ToString(), StringComparer.Ordinal))
        {
            text.Append(' ').Append(attribute.Name).Append("=\"").Append(attribute.Value).Append('"');
        }

        text.Append('>');
        foreach (XNode node in element.Nodes())
        {
            if (node is XElement child)
            {
                Write(text, child);
            }
            else
            {
                text.Append(node);
            }
        }

        text.Append("</").Append(element.Name.LocalName).Append('>');
    }

    private sealed class Reveal
    {
        public MapBounds View { get; init; }

        public IReadOnlyList<IReadOnlyList<MapPoint>> Regions { get; init; }

        public IReadOnlyList<MapBounds> Cells { get; init; }

        public IReadOnlyList<FeatureMarker> Markers { get; init; }

        public DoorRenderState Doors { get; init; }

        public ConcealState Conceal { get; init; }
    }
}
