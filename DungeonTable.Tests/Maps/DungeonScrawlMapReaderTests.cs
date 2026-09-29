using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Infrastructure.Maps;

namespace DungeonTable.Tests.Maps;

/// <summary>
/// Verifies the Dungeon Scrawl reader against the sample pack's <c>.ds</c> file, which was drawn to
/// use each thing the reader handles once (a floor with holes, doors in both orientations, stairs
/// with their step lines, imported sprites, one of them mirrored), and against synthetic malformed
/// archives.
/// </summary>
public sealed class DungeonScrawlMapReaderTests
{
    // The sprites the map's five placements were imported under; the two statues share one.
    private static readonly string[] DecorationNames = { "chest", "furnace", "pit", "statue", "statue" };

    private static async Task<VectorMap> LoadSample()
    {
        var result = await new DungeonScrawlMapReader().ReadFileAsync(SamplePack.MapFile, CancellationToken.None);
        Assert.True(result.IsValid, string.Join("; ", result.Messages.Select(m => m.Message)));
        return result.Value;
    }

    [Fact]
    public void The_sample_map_source_file_exists()
    {
        Assert.True(File.Exists(SamplePack.MapFile), $"Dungeon Scrawl source not found: {SamplePack.MapFile}");
    }

    [Fact]
    public async Task Reads_the_map_id_and_adventure_from_its_path()
    {
        VectorMap map = await LoadSample();
        Assert.Equal("level-1-undercroft", map.MapId);
        Assert.Equal("demo", map.Adventure);
    }

    [Fact]
    public async Task Layer_stack_is_in_z_order_bottom_first()
    {
        VectorMap map = await LoadSample();
        LayerKind[] kinds = map.Layers.Select(layer => layer.Kind).ToArray();
        Assert.Equal(
            new[] { LayerKind.BufferShading, LayerKind.Hatching, LayerKind.Floor, LayerKind.Folder, LayerKind.Walls },
            kinds);
    }

    [Fact]
    public async Task Floor_layer_fills_parchment_and_carries_the_main_geometry()
    {
        VectorMap map = await LoadSample();
        MapLayer floor = map.Layers.Single(layer => layer.Kind == LayerKind.Floor);

        Assert.True(floor.Fill.Visible);
        Assert.False(floor.Stroke.Visible);
        Assert.Equal(new Rgba(240, 236, 224, 255), floor.Fill.Colour);

        // The whole level's outline lives in the one shared blob (invariant: it has polygons).
        Assert.NotEmpty(floor.Geometry.Polygons);
    }

    [Fact]
    public async Task Walls_layer_strokes_black_and_shares_the_main_geometry()
    {
        VectorMap map = await LoadSample();
        MapLayer walls = map.Layers.Single(layer => layer.Kind == LayerKind.Walls);

        Assert.True(walls.Stroke.Visible);
        Assert.False(walls.Fill.Visible);
        Assert.Equal(5, walls.Stroke.Width);
        Assert.Equal(new Rgba(0, 0, 0, 255), walls.Stroke.Colour);
        Assert.Equal(RoughLevel.Low, walls.Stroke.Rough);

        // Walls and Floor restyle the SAME shared blob (invariant: equal polygon counts).
        MapLayer floor = map.Layers.Single(layer => layer.Kind == LayerKind.Floor);
        Assert.Equal(floor.Geometry.Polygons.Count, walls.Geometry.Polygons.Count);
        Assert.NotEmpty(walls.Geometry.Polygons);
    }

    [Fact]
    public async Task Folder_clips_grid_and_shadow_to_its_mask()
    {
        VectorMap map = await LoadSample();
        MapLayer folder = map.Layers.Single(layer => layer.Kind == LayerKind.Folder);

        LayerKind[] childKinds = folder.Children.Select(child => child.Kind).ToArray();
        Assert.Equal(new[] { LayerKind.Mask, LayerKind.Shadow, LayerKind.Grid }, childKinds);
        Assert.True(folder.Children.Single(child => child.Kind == LayerKind.Mask).IsMask);
        Assert.False(folder.ClipMask.IsEmpty);
    }

    [Fact]
    public async Task Reads_doors_and_stairs_as_objects()
    {
        VectorMap map = await LoadSample();

        // The five doors and two stairs the map was drawn with.
        Assert.Equal(5, map.Doors.Count);
        Assert.Equal(2, map.Stairs.Count);
    }

    [Fact]
    public async Task Every_door_has_world_space_geometry_and_a_derived_orientation()
    {
        VectorMap map = await LoadSample();
        foreach (DoorFeature door in map.Doors)
        {
            Assert.False(door.Geometry.IsEmpty);
            Assert.NotEqual(DoorOrientation.None, door.Orientation);
            // Door vertices are absolute (identity transform): they sit inside the level bounds.
            Assert.True(door.Bounds.MinX >= map.Bounds.MinX - 1 && door.Bounds.MaxX <= map.Bounds.MaxX + 1);
        }
    }

    [Fact]
    public async Task Door_orientation_is_derived_from_the_leaf_aspect()
    {
        VectorMap map = await LoadSample();

        // Two doors stand in north-south walls and three in east-west ones.
        Assert.Equal(2, map.Doors.Count(door => door.Orientation == DoorOrientation.Vertical));
        Assert.Equal(3, map.Doors.Count(door => door.Orientation == DoorOrientation.Horizontal));

        // Lock the rule itself: horizontal iff the leaf is at least as wide as it is tall.
        Assert.All(map.Doors, door => Assert.Equal(
            door.Bounds.Width >= door.Bounds.Height ? DoorOrientation.Horizontal : DoorOrientation.Vertical,
            door.Orientation));
    }

    [Fact]
    public async Task Floor_geometry_preserves_polygon_holes()
    {
        VectorMap map = await LoadSample();
        MapLayer floor = map.Layers.Single(layer => layer.Kind == LayerKind.Floor);

        // The nave, with its two pillars erased out of its floor: an outer ring and two holes. A
        // reader that flattened holes or split rings into separate polygons would lose this.
        Assert.Contains(floor.Geometry.Polygons, polygon => polygon.Rings.Count == 3);
    }

    [Fact]
    public async Task Rings_are_kept_closed_and_not_re_closed()
    {
        VectorMap map = await LoadSample();
        MapLayer floor = map.Layers.Single(layer => layer.Kind == LayerKind.Floor);
        PolygonRing outer = floor.Geometry.Polygons.First(polygon => polygon.Rings.Count > 1).Rings[0];

        // The source rings are already closed (first == last); the reader must keep them as-is.
        Assert.True(outer.Vertices.Count >= 4);
        Assert.Equal(outer.Vertices[0], outer.Vertices[^1]);
    }

    [Fact]
    public async Task Stairs_are_polyline_only()
    {
        VectorMap map = await LoadSample();
        Assert.All(map.Stairs, stair => Assert.Empty(stair.Geometry.Polygons));

        // Both of this map's stairs were drawn with their step lines.
        Assert.All(map.Stairs, stair => Assert.NotEmpty(stair.Geometry.Polylines));
    }

    [Fact]
    public async Task Grid_layer_decodes_its_clean_stroke()
    {
        VectorMap map = await LoadSample();
        MapLayer folder = map.Layers.Single(layer => layer.Kind == LayerKind.Folder);
        MapLayer grid = folder.Children.Single(child => child.Kind == LayerKind.Grid);

        Assert.True(grid.Stroke.Visible);
        Assert.Equal(1, grid.Stroke.Width);
        Assert.Equal(new Rgba(85, 85, 85, 255), grid.Stroke.Colour);
    }

    [Fact]
    public async Task Bounds_span_the_drawn_level()
    {
        VectorMap map = await LoadSample();
        Assert.True(map.Bounds.Width > 0 && double.IsFinite(map.Bounds.Width));
        Assert.True(map.Bounds.Height > 0 && double.IsFinite(map.Bounds.Height));
    }

    [Fact]
    public async Task Reads_decoration_objects_and_deduped_sprites()
    {
        VectorMap map = await LoadSample();

        // Five placements of four sprites: the second statue is a copy of the first, not a second
        // import, so its sprite is stored once.
        Assert.Equal(5, map.Decorations.Count);
        Assert.Equal(4, map.Sprites.Count);
        Assert.Equal(map.Sprites.Select(sprite => sprite.AssetId).Distinct().Count(), map.Sprites.Count);
    }

    [Fact]
    public async Task Decorations_carry_the_names_their_sprites_were_imported_under()
    {
        VectorMap map = await LoadSample();
        Assert.Equal(DecorationNames, map.Decorations.Select(d => d.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_mirrored_placement_keeps_its_bounds_the_right_way_round()
    {
        VectorMap map = await LoadSample();

        // The nave's statue is flipped: a negative scale term in its transform, which must not turn
        // its bounds inside out, since the Room Editor hit-tests a placement by them.
        MapDecoration mirrored = Assert.Single(map.Decorations, d => d.Transform.A < 0);
        Assert.Equal("statue", mirrored.Name);
        Assert.InRange(mirrored.Bounds.Width, 1, 1000);
        Assert.InRange(mirrored.Bounds.Height, 1, 1000);
    }

    [Fact]
    public async Task Decorations_carry_a_unique_id_and_world_bounds()
    {
        // The Room Editor annotates an individual placement (to conceal it), so every decoration
        // needs a stable id to key that annotation to and bounds to hit-test it by.
        VectorMap map = await LoadSample();

        Assert.All(map.Decorations, d => Assert.NotEmpty(d.Id));
        Assert.Equal(map.Decorations.Count, map.Decorations.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count());
        // A decoration is a sprite of a cell or two, placed inside the map — never inverted by a
        // negative scale term in its transform, and never degenerate.
        Assert.All(map.Decorations, d => Assert.InRange(d.Bounds.Width, 1, 1000));
        Assert.All(map.Decorations, d => Assert.InRange(d.Bounds.Height, 1, 1000));
    }

    [Fact]
    public async Task Sprites_are_embedded_png_data_uris_at_object_scale()
    {
        VectorMap map = await LoadSample();
        Assert.All(map.Sprites, sprite => Assert.StartsWith("data:image/png;base64,", sprite.DataUri));
        // Decoration sprites are a cell or two across (~36-76 units), never the 6000+ unit scan.
        Assert.All(map.Sprites, sprite => Assert.InRange(sprite.Width, 1, 1000));
    }

    [Fact]
    public async Task Background_scan_is_excluded_from_decorations()
    {
        VectorMap map = await LoadSample();
        Assert.DoesNotContain(map.Decorations, d => d.Name.Contains("extracted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Grid_is_calibrated_from_the_page_cell_diameter()
    {
        VectorMap map = await LoadSample();
        // Cell size comes straight from PAGE.grid.cellDiameter; extent depends on the drawing.
        Assert.Equal(36, map.Grid.CellSizePx);
        Assert.True(map.Grid.Cols > 0);
        Assert.True(map.Grid.Rows > 0);
        // Origin is snapped to the cell grid.
        Assert.Equal(0, map.Grid.OriginX % 36, 3);
        Assert.Equal(0, map.Grid.OriginY % 36, 3);
    }

    [Fact]
    public async Task Missing_file_is_invalid()
    {
        var reader = new DungeonScrawlMapReader();
        var result = await reader.ReadFileAsync("does-not-exist.ds", CancellationToken.None);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Messages);
    }

    [Fact]
    public void Empty_content_is_invalid()
    {
        Assert.False(new DungeonScrawlMapReader().Read(Array.Empty<byte>(), "m", "a").IsValid);
    }

    [Fact]
    public void Non_zip_content_is_invalid_rather_than_throwing()
    {
        byte[] garbage = Encoding.UTF8.GetBytes("this is not a zip archive");
        Assert.False(new DungeonScrawlMapReader().Read(garbage, "m", "a").IsValid);
    }

    [Fact]
    public void Archive_without_a_map_entry_is_invalid()
    {
        byte[] ds = MakeArchive("{\"version\":1}", entryName: "notmap");
        Assert.False(new DungeonScrawlMapReader().Read(ds, "m", "a").IsValid);
    }

    [Fact]
    public void Unsupported_version_is_invalid()
    {
        byte[] ds = MakeArchive("{\"version\":2,\"state\":{\"document\":{\"nodes\":{}}}}");
        Assert.False(new DungeonScrawlMapReader().Read(ds, "m", "a").IsValid);
    }

    [Fact]
    public void Missing_nodes_is_invalid()
    {
        byte[] ds = MakeArchive("{\"version\":1,\"state\":{\"document\":{}}}");
        Assert.False(new DungeonScrawlMapReader().Read(ds, "m", "a").IsValid);
    }

    [Fact]
    public void Invalid_json_map_entry_is_invalid()
    {
        byte[] ds = MakeArchive("{ this is not valid json ");
        Assert.False(new DungeonScrawlMapReader().Read(ds, "m", "a").IsValid);
    }

    [Fact]
    public void Assets_on_a_hidden_images_layer_are_excluded()
    {
        const string json = """
            {
              "version": 1,
              "state": { "document": { "documentNodeId": "document", "nodes": {
                "document": { "type": "DOCUMENT", "id": "document", "selectedPage": "page" },
                "page": { "type": "PAGE", "id": "page", "children": ["vis", "hid"], "grid": { "cellDiameter": 36 } },
                "vis": { "type": "IMAGES", "id": "vis", "parentId": "page", "visible": true, "children": ["a1"] },
                "hid": { "type": "IMAGES", "id": "hid", "parentId": "page", "visible": false, "children": ["a2"] },
                "a1": { "type": "ASSET", "id": "a1", "parentId": "vis", "assetId": "s", "visible": true, "transform": [1,0,0,1,0,0] },
                "a2": { "type": "ASSET", "id": "a2", "parentId": "hid", "assetId": "s", "visible": true, "transform": [1,0,0,1,0,0] }
              } } },
              "data": { "geometry": {}, "assets": {
                "s": { "asset": { "dimensions": [70,70], "scale": [0.5,0.5] }, "data": "data:text/plain;base64,iVBORAAA" }
              } }
            }
            """;

        var result = new DungeonScrawlMapReader().Read(MakeArchive(json), "m", "a");

        Assert.True(result.IsValid);
        // Only the asset on the visible layer survives; the sprite is deduped once.
        Assert.Single(result.Value.Decorations);
        Assert.Single(result.Value.Sprites);
    }

    [Fact]
    public void Non_object_node_and_asset_values_do_not_throw()
    {
        // A bare number node and a null asset blob would make the JSON helpers throw without the
        // ValueKind guards; the reader must degrade rather than crash.
        const string json = """
            {
              "version": 1,
              "state": { "document": { "documentNodeId": "document", "nodes": {
                "document": { "type": "DOCUMENT", "id": "document", "selectedPage": "page" },
                "page": { "type": "PAGE", "id": "page", "children": ["imgs"], "grid": { "cellDiameter": 36 } },
                "imgs": { "type": "IMAGES", "id": "imgs", "parentId": "page", "visible": true, "children": ["a"] },
                "a": { "type": "ASSET", "id": "a", "parentId": "imgs", "assetId": "x", "visible": true, "transform": [1,0,0,1,0,0] },
                "junk": 5
              } } },
              "data": { "geometry": {}, "assets": { "x": null } }
            }
            """;

        var result = new DungeonScrawlMapReader().Read(MakeArchive(json), "m", "a");

        Assert.True(result.IsValid);
        // The null asset blob resolves to nothing, so its decoration is dropped.
        Assert.Empty(result.Value.Decorations);
    }

    [Fact]
    public void Cyclic_folder_graph_completes_instead_of_overflowing_the_stack()
    {
        // A folder that lists itself as a child would recurse forever without the depth guard,
        // throwing an uncatchable StackOverflowException; the reader must terminate normally.
        const string json = """
            {
              "version": 1,
              "state": { "document": { "documentNodeId": "document", "nodes": {
                "document": { "type": "DOCUMENT", "id": "document", "selectedPage": "page" },
                "page": { "type": "PAGE", "id": "page", "children": ["tmpl"], "grid": { "cellDiameter": 36 } },
                "tmpl": { "type": "TEMPLATE", "id": "tmpl", "parentId": "page", "children": ["geo"] },
                "geo": { "type": "GEOMETRY", "id": "geo", "parentId": "tmpl", "geometryId": "g1", "children": ["folder"] },
                "folder": { "type": "FOLDER", "id": "folder", "parentId": "geo", "children": ["folder"] }
              } } },
              "data": { "geometry": { "g1": { "polygons": [], "polylines": [] } } }
            }
            """;

        var result = new DungeonScrawlMapReader().Read(MakeArchive(json), "m", "a");

        Assert.True(result.IsValid);
    }

    private static byte[] MakeArchive(string mapJson, string entryName = "map")
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName);
            using Stream entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(mapJson);
        }

        return buffer.ToArray();
    }
}
