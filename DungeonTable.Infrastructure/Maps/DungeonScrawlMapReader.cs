using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Infrastructure.Maps;

/// <summary>
/// Reads a Dungeon Scrawl <c>.ds</c> file into a fully-vector <see cref="VectorMap"/>.
/// A <c>.ds</c> is a ZIP archive whose single <c>map</c> entry is JSON: a flat node graph
/// (<c>state.document.nodes</c>) plus shared vertex blobs (<c>data.geometry</c>). The
/// reader resolves the template layer stack (floor, walls, effects, grid) and the
/// door/stairs object layer, and derives grid calibration from <c>PAGE.grid.cellDiameter</c>.
/// Malformed input yields an invalid <see cref="Result{T}"/> rather than an exception.
/// Immutable and stateless, so it is safe to use as a singleton.
/// </summary>
public sealed class DungeonScrawlMapReader : IVectorMapReader
{
    private const string MapEntryName = "map";

    // Bounds folder recursion so a malformed file with a self- or mutually-referencing
    // folder graph fails as an invalid Result rather than a StackOverflowException. Real
    // template folders nest only a level or two.
    private const int MaxLayerDepth = 64;

    /// <summary>
    /// Reads a <c>.ds</c> file from disk. The map id is the file name stem and the adventure
    /// is the parent folder name.
    /// </summary>
    /// <param name="filePath">Absolute path to the <c>.ds</c> file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed map, or an invalid result when the file is missing or malformed.</returns>
    public async Task<Result<VectorMap>> ReadFileAsync(string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Invalid("A .ds file path is required.");
        }

        if (!File.Exists(filePath))
        {
            return Invalid($"Dungeon Scrawl file '{filePath}' was not found.");
        }

        try
        {
            // Read the whole file into memory first: the ZIP needs a seekable stream, and the
            // on-disk name can change mid-session while the zip entry stays 'map'.
            byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
            return Read(bytes, MapIdFromPath(filePath), AdventureFromPath(filePath));
        }
        catch (IOException exception)
        {
            return Invalid($"Dungeon Scrawl file '{filePath}' could not be read: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Invalid($"Dungeon Scrawl file '{filePath}' could not be read: {exception.Message}");
        }
    }

    /// <summary>Reads a <c>.ds</c> archive from an in-memory byte array.</summary>
    /// <param name="dsContent">The raw <c>.ds</c> (ZIP) bytes.</param>
    /// <param name="mapId">Stable map id to assign.</param>
    /// <param name="adventure">Adventure to assign.</param>
    /// <returns>The parsed map, or an invalid result when the archive is malformed.</returns>
    public Result<VectorMap> Read(byte[] dsContent, string mapId, string adventure)
    {
        if (dsContent is null || dsContent.Length == 0)
        {
            return Invalid("The .ds content is empty.");
        }

        byte[] mapJson;
        try
        {
            using var archive = new ZipArchive(new MemoryStream(dsContent), ZipArchiveMode.Read);
            ZipArchiveEntry entry = archive.GetEntry(MapEntryName);
            if (entry is null)
            {
                return Invalid("The .ds archive does not contain a 'map' entry.");
            }

            using Stream entryStream = entry.Open();
            using var buffer = new MemoryStream();
            entryStream.CopyTo(buffer);
            mapJson = buffer.ToArray();
        }
        catch (InvalidDataException exception)
        {
            return Invalid($"The .ds archive is not a valid ZIP: {exception.Message}");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(mapJson);
            return Build(document.RootElement, mapId, adventure);
        }
        catch (JsonException exception)
        {
            return Invalid($"The .ds 'map' entry is not valid JSON: {exception.Message}");
        }
    }

    private static Result<VectorMap> Build(JsonElement root, string mapId, string adventure)
    {
        if (!(root.TryGetProperty("version", out JsonElement version)
            && version.ValueKind == JsonValueKind.Number
            && version.TryGetInt32(out int versionNumber)
            && versionNumber == 1))
        {
            return Invalid("Unsupported .ds version (expected version 1).");
        }

        if (!(root.TryGetProperty("state", out JsonElement state)
            && state.TryGetProperty("document", out JsonElement documentRoot)
            && documentRoot.TryGetProperty("nodes", out JsonElement nodesElement)
            && nodesElement.ValueKind == JsonValueKind.Object))
        {
            return Invalid("The .ds file is missing state.document.nodes.");
        }

        Dictionary<string, JsonElement> nodes = new(StringComparer.Ordinal);
        foreach (JsonProperty property in nodesElement.EnumerateObject())
        {
            nodes[property.Name] = property.Value;
        }

        Dictionary<string, VectorGeometry> geometry = BuildGeometryStore(root);

        string documentNodeId = Str(documentRoot, "documentNodeId", "document");
        if (!nodes.TryGetValue(documentNodeId, out JsonElement documentNode))
        {
            return Invalid("The .ds file has no document node.");
        }

        string pageId = Str(documentNode, "selectedPage");
        if (!nodes.TryGetValue(pageId, out JsonElement pageNode))
        {
            return Invalid("The .ds file has no selected page.");
        }

        double cellDiameter = 0;
        if (pageNode.TryGetProperty("grid", out JsonElement pageGrid))
        {
            cellDiameter = Dbl(pageGrid, "cellDiameter");
        }

        IReadOnlyList<MapLayer> layers = BuildLayerStack(nodes, geometry, pageNode);
        (IReadOnlyList<DoorFeature> doors, IReadOnlyList<StairFeature> stairs) = BuildObjects(nodes, geometry);
        (IReadOnlyList<MapDecoration> decorations, IReadOnlyList<SpriteImage> sprites) = BuildDecorations(nodes, root);

        MapBounds bounds = OverallBounds(geometry.Values);
        Grid grid = BuildGrid(bounds, cellDiameter);

        VectorMap map = new()
        {
            MapId = mapId,
            Adventure = adventure,
            Bounds = bounds,
            Grid = grid,
            Layers = layers,
            Doors = doors,
            Stairs = stairs,
            Decorations = decorations,
            Sprites = sprites,
        };
        return Result.For(map);
    }

    private static Dictionary<string, VectorGeometry> BuildGeometryStore(JsonElement root)
    {
        Dictionary<string, VectorGeometry> store = new(StringComparer.Ordinal);
        if (root.TryGetProperty("data", out JsonElement data)
            && data.TryGetProperty("geometry", out JsonElement geometry)
            && geometry.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in geometry.EnumerateObject())
            {
                store[property.Name] = ReadGeometry(property.Value);
            }
        }

        return store;
    }

    private static VectorGeometry ReadGeometry(JsonElement blob)
    {
        List<MapPolygon> polygons = new();
        if (blob.TryGetProperty("polygons", out JsonElement polygonsElement)
            && polygonsElement.ValueKind == JsonValueKind.Array)
        {
            // Depth 4: polygon -> ring -> vertex -> [x, y].
            foreach (JsonElement polygon in polygonsElement.EnumerateArray())
            {
                if (polygon.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                List<PolygonRing> rings = new();
                foreach (JsonElement ring in polygon.EnumerateArray())
                {
                    rings.Add(new PolygonRing { Vertices = ReadPoints(ring) });
                }

                polygons.Add(new MapPolygon { Rings = rings });
            }
        }

        List<Polyline> polylines = new();
        if (blob.TryGetProperty("polylines", out JsonElement polylinesElement)
            && polylinesElement.ValueKind == JsonValueKind.Array)
        {
            // Depth 3: polyline -> point -> [x, y].
            foreach (JsonElement polyline in polylinesElement.EnumerateArray())
            {
                polylines.Add(new Polyline { Points = ReadPoints(polyline) });
            }
        }

        if (polygons.Count == 0 && polylines.Count == 0)
        {
            return VectorGeometry.Empty;
        }

        return new VectorGeometry { Polygons = polygons, Polylines = polylines };
    }

    private static IReadOnlyList<MapPoint> ReadPoints(JsonElement pointArray)
    {
        if (pointArray.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MapPoint>();
        }

        List<MapPoint> points = new();
        foreach (JsonElement point in pointArray.EnumerateArray())
        {
            if (point.ValueKind == JsonValueKind.Array
                && point.GetArrayLength() >= 2
                && point[0].ValueKind == JsonValueKind.Number
                && point[1].ValueKind == JsonValueKind.Number)
            {
                double x = point[0].GetDouble();
                double y = point[1].GetDouble();
                if (double.IsFinite(x) && double.IsFinite(y))
                {
                    points.Add(new MapPoint(x, y));
                }
            }
        }

        return points;
    }

    private static IReadOnlyList<MapLayer> BuildLayerStack(
        IReadOnlyDictionary<string, JsonElement> nodes,
        IReadOnlyDictionary<string, VectorGeometry> geometry,
        JsonElement pageNode)
    {
        // The drawing lives under the first TEMPLATE -> GEOMETRY on the page; its children,
        // in array order, are the z-ordered layer stack.
        foreach (JsonElement pageChild in ChildNodes(nodes, pageNode))
        {
            if (TypeOf(pageChild) != "TEMPLATE")
            {
                continue;
            }

            foreach (JsonElement templateChild in ChildNodes(nodes, pageChild))
            {
                if (TypeOf(templateChild) != "GEOMETRY")
                {
                    continue;
                }

                List<MapLayer> layers = new();
                int z = 0;
                foreach (JsonElement layerNode in ChildNodes(nodes, templateChild))
                {
                    layers.Add(BuildLayer(nodes, geometry, layerNode, z++, depth: 0));
                }

                return layers;
            }
        }

        return Array.Empty<MapLayer>();
    }

    private static MapLayer BuildLayer(
        IReadOnlyDictionary<string, JsonElement> nodes,
        IReadOnlyDictionary<string, VectorGeometry> geometry,
        JsonElement node,
        int zOrder,
        int depth)
    {
        string type = TypeOf(node);
        string name = Str(node, "name");
        bool visible = Bool(node, "visible", true);
        double alpha = Dbl(node, "alpha", 1);

        switch (type)
        {
            case "MULTIPOLYGON":
            {
                bool isMask = Bool(node, "mask");
                bool isFog = Bool(node, "isFog");
                bool allowsLight = false;
                VectorGeometry geo = VectorGeometry.Empty;
                if (TryClimbToGeometry(nodes, Str(node, "id"), out JsonElement owner))
                {
                    allowsLight = Bool(owner, "allowsLightToPass");
                    geo = ResolveGeometry(geometry, owner);
                }

                return new MapLayer
                {
                    Name = name,
                    Kind = ClassifyMultipolygon(isMask, name),
                    ZOrder = zOrder,
                    Visible = visible,
                    Alpha = alpha,
                    IsFog = isFog,
                    IsMask = isMask,
                    AllowsLightToPass = allowsLight,
                    Geometry = geo,
                    Stroke = ReadStroke(node),
                    Fill = ReadFill(node),
                };
            }

            case "FOLDER":
            {
                List<MapLayer> children = new();
                VectorGeometry clip = VectorGeometry.Empty;

                // Stop descending past the depth cap: a cyclic folder graph would otherwise
                // recurse until the stack overflows (an exception the caller cannot catch).
                if (depth < MaxLayerDepth)
                {
                    int childZ = 0;
                    foreach (JsonElement childNode in ChildNodes(nodes, node))
                    {
                        MapLayer child = BuildLayer(nodes, geometry, childNode, childZ++, depth + 1);
                        children.Add(child);
                        if (child.IsMask)
                        {
                            clip = child.Geometry;
                        }
                    }
                }

                return new MapLayer
                {
                    Name = name,
                    Kind = LayerKind.Folder,
                    ZOrder = zOrder,
                    Visible = visible,
                    Alpha = alpha,
                    Children = children,
                    ClipMask = clip,
                };
            }

            case "GRID":
                return new MapLayer
                {
                    Name = name,
                    Kind = LayerKind.Grid,
                    ZOrder = zOrder,
                    Visible = visible,
                    Alpha = alpha,
                    Stroke = ReadGridStroke(node),
                };

            case "BUFFER_SHADING":
                return EffectLayer(name, LayerKind.BufferShading, zOrder, visible, alpha);

            case "HATCHING":
                return EffectLayer(name, LayerKind.Hatching, zOrder, visible, alpha);

            case "SHADOW":
                return EffectLayer(name, LayerKind.Shadow, zOrder, visible, alpha);

            default:
                return EffectLayer(name, LayerKind.None, zOrder, visible, alpha);
        }
    }

    private static LayerKind ClassifyMultipolygon(bool isMask, string name)
    {
        if (isMask)
        {
            return LayerKind.Mask;
        }

        return name switch
        {
            "Floor" => LayerKind.Floor,
            "Walls" => LayerKind.Walls,
            _ => LayerKind.None,
        };
    }

    private static MapLayer EffectLayer(string name, LayerKind kind, int zOrder, bool visible, double alpha) =>
        new()
        {
            Name = name,
            Kind = kind,
            ZOrder = zOrder,
            Visible = visible,
            Alpha = alpha,
        };

    private static (IReadOnlyList<DoorFeature> Doors, IReadOnlyList<StairFeature> Stairs) BuildObjects(
        IReadOnlyDictionary<string, JsonElement> nodes,
        IReadOnlyDictionary<string, VectorGeometry> geometry)
    {
        List<DoorFeature> doors = new();
        List<StairFeature> stairs = new();

        foreach (KeyValuePair<string, JsonElement> entry in nodes)
        {
            JsonElement node = entry.Value;
            if (TypeOf(node) != "DUNGEON_ASSET")
            {
                continue;
            }

            string name = Str(node, "name");
            string id = Str(node, "id", entry.Key);

            // The object's shape is its first child GEOMETRY, already in world coordinates
            // (all object transforms are identity), so no matrix is applied.
            VectorGeometry geo = FirstChildGeometry(nodes, geometry, node);
            MapBounds bounds = BoundsOf(geo);
            if (name == "Stairs")
            {
                stairs.Add(new StairFeature { Id = id, Geometry = geo, Bounds = bounds });
            }
            else if (name == "Door")
            {
                doors.Add(new DoorFeature
                {
                    Id = id,
                    Geometry = geo,
                    Orientation = OrientationOf(bounds),
                    Bounds = bounds,
                });
            }
        }

        return (doors, stairs);
    }

    private static (IReadOnlyList<MapDecoration> Decorations, IReadOnlyList<SpriteImage> Sprites) BuildDecorations(
        IReadOnlyDictionary<string, JsonElement> nodes,
        JsonElement root)
    {
        List<MapDecoration> decorations = new();
        HashSet<string> referenced = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, JsonElement> entry in nodes)
        {
            JsonElement node = entry.Value;
            if (TypeOf(node) != "ASSET" || !Bool(node, "visible", true))
            {
                continue;
            }

            // Exclude sprites on a hidden IMAGES layer (the traced background scan lives there).
            if (TryClimbToType(nodes, Str(node, "parentId"), "IMAGES", out JsonElement images)
                && !Bool(images, "visible", true))
            {
                continue;
            }

            string assetId = Str(node, "assetId");
            if (assetId.Length == 0)
            {
                continue;
            }

            decorations.Add(new MapDecoration
            {
                Id = Str(node, "id", entry.Key),
                Name = Str(node, "name"),
                AssetId = assetId,
                Transform = ReadTransform(node),
                Alpha = Dbl(node, "alpha", 1),
            });
            referenced.Add(assetId);
        }

        IReadOnlyList<SpriteImage> sprites = BuildSprites(root, referenced);

        // Keep only placements whose sprite was actually resolved, and stamp each with the world
        // bounds of its placed sprite (the sprite's own size is only known once BuildSprites has run).
        Dictionary<string, SpriteImage> byAssetId = sprites.ToDictionary(sprite => sprite.AssetId, StringComparer.Ordinal);
        IReadOnlyList<MapDecoration> placed = decorations
            .Where(decoration => byAssetId.ContainsKey(decoration.AssetId))
            .Select(decoration => new MapDecoration
            {
                Id = decoration.Id,
                Name = decoration.Name,
                AssetId = decoration.AssetId,
                Transform = decoration.Transform,
                Alpha = decoration.Alpha,
                Bounds = PlacedBounds(decoration.Transform, byAssetId[decoration.AssetId]),
            })
            .ToList();

        return (placed, sprites);
    }

    // The world-space bounding box of a sprite placed by an affine transform: the sprite's local box
    // (0,0)-(width,height) run through the matrix, then the min/max of the four mapped corners. The
    // corners are taken individually because a placement may flip or rotate the sprite (a negative
    // scale term is common in the source files), which would otherwise invert the box.
    private static MapBounds PlacedBounds(AffineTransform t, SpriteImage sprite)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach ((double localX, double localY) in new[]
        {
            (0d, 0d), (sprite.Width, 0d), (0d, sprite.Height), (sprite.Width, sprite.Height),
        })
        {
            double x = (t.A * localX) + (t.C * localY) + t.E;
            double y = (t.B * localX) + (t.D * localY) + t.F;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        return new MapBounds(minX, minY, maxX, maxY);
    }

    private static List<SpriteImage> BuildSprites(JsonElement root, HashSet<string> assetIds)
    {
        List<SpriteImage> sprites = new();
        if (assetIds.Count == 0
            || !root.TryGetProperty("data", out JsonElement data)
            || !data.TryGetProperty("assets", out JsonElement assets)
            || assets.ValueKind != JsonValueKind.Object)
        {
            return sprites;
        }

        foreach (string assetId in assetIds)
        {
            if (!assets.TryGetProperty(assetId, out JsonElement blob))
            {
                continue;
            }

            string raw = Str(blob, "data");
            int comma = raw.IndexOf(',');
            string base64 = comma >= 0 ? raw[(comma + 1)..] : raw;
            if (base64.Length == 0)
            {
                continue;
            }

            JsonElement meta = blob.ValueKind == JsonValueKind.Object && blob.TryGetProperty("asset", out JsonElement m)
                ? m
                : default;
            double worldWidth = ArrayElement(meta, "dimensions", 0, 0) * ArrayElement(meta, "scale", 0, 1);
            double worldHeight = ArrayElement(meta, "dimensions", 1, 0) * ArrayElement(meta, "scale", 1, 1);

            // Skip degenerate sprites so a kept decoration never references a sprite the renderer
            // would drop (the resolved-set filter below then drops those decorations too).
            if (worldWidth <= 0 || worldHeight <= 0)
            {
                continue;
            }

            sprites.Add(new SpriteImage
            {
                AssetId = assetId,
                DataUri = "data:" + SniffMime(base64) + ";base64," + base64,
                Width = worldWidth,
                Height = worldHeight,
            });
        }

        return sprites;
    }

    private static string SniffMime(string base64)
    {
        if (base64.StartsWith("iVBOR", StringComparison.Ordinal))
        {
            return "image/png";
        }

        if (base64.StartsWith("/9j/", StringComparison.Ordinal))
        {
            return "image/jpeg";
        }

        return "image/png";
    }

    private static AffineTransform ReadTransform(JsonElement node)
    {
        if (node.TryGetProperty("transform", out JsonElement t)
            && t.ValueKind == JsonValueKind.Array
            && t.GetArrayLength() >= 6)
        {
            return new AffineTransform(
                Num(t, 0), Num(t, 1), Num(t, 2), Num(t, 3), Num(t, 4), Num(t, 5));
        }

        return AffineTransform.Identity;
    }

    private static double Num(JsonElement array, int index) =>
        array[index].ValueKind == JsonValueKind.Number ? array[index].GetDouble() : 0;

    private static double ArrayElement(JsonElement node, string property, int index, double fallback)
    {
        if (node.ValueKind == JsonValueKind.Object
            && node.TryGetProperty(property, out JsonElement array)
            && array.ValueKind == JsonValueKind.Array
            && array.GetArrayLength() > index
            && array[index].ValueKind == JsonValueKind.Number)
        {
            return array[index].GetDouble();
        }

        return fallback;
    }

    private static bool TryClimbToType(
        IReadOnlyDictionary<string, JsonElement> nodes,
        string startId,
        string type,
        out JsonElement found)
    {
        found = default;
        string current = startId;
        int guard = 0;
        while (!string.IsNullOrEmpty(current) && guard++ < 10_000 && nodes.TryGetValue(current, out JsonElement node))
        {
            if (TypeOf(node) == type)
            {
                found = node;
                return true;
            }

            current = Str(node, "parentId");
        }

        return false;
    }

    private static DoorOrientation OrientationOf(MapBounds bounds)
    {
        if (bounds.IsEmpty)
        {
            return DoorOrientation.None;
        }

        return bounds.Width >= bounds.Height ? DoorOrientation.Horizontal : DoorOrientation.Vertical;
    }

    private static Grid BuildGrid(MapBounds bounds, double cellDiameter)
    {
        double cell = cellDiameter;
        int cols = cell > 0 ? (int)Math.Round(bounds.Width / cell) : 0;
        int rows = cell > 0 ? (int)Math.Round(bounds.Height / cell) : 0;

        return new Grid
        {
            OriginX = Snap(bounds.MinX, cell),
            OriginY = Snap(bounds.MinY, cell),
            CellSizePx = cell,
            Cols = cols,
            Rows = rows,
        };
    }

    private static double Snap(double value, double cell) =>
        cell > 0 ? Math.Round(value / cell) * cell : value;

    private static VectorGeometry FirstChildGeometry(
        IReadOnlyDictionary<string, JsonElement> nodes,
        IReadOnlyDictionary<string, VectorGeometry> geometry,
        JsonElement node)
    {
        JsonElement child = ChildNodes(nodes, node).FirstOrDefault(candidate => TypeOf(candidate) == "GEOMETRY");
        return child.ValueKind == JsonValueKind.Object
            ? ResolveGeometry(geometry, child)
            : VectorGeometry.Empty;
    }

    private static VectorGeometry ResolveGeometry(
        IReadOnlyDictionary<string, VectorGeometry> geometry,
        JsonElement geometryNode) =>
        geometry.TryGetValue(Str(geometryNode, "geometryId"), out VectorGeometry geo)
            ? geo
            : VectorGeometry.Empty;

    private static bool TryClimbToGeometry(
        IReadOnlyDictionary<string, JsonElement> nodes,
        string startId,
        out JsonElement geometryNode)
    {
        geometryNode = default;
        string current = startId;
        int guard = 0;
        while (!string.IsNullOrEmpty(current) && guard++ < 10_000 && nodes.TryGetValue(current, out JsonElement node))
        {
            if (TypeOf(node) == "GEOMETRY")
            {
                geometryNode = node;
                return true;
            }

            current = Str(node, "parentId");
        }

        return false;
    }

    private static IEnumerable<JsonElement> ChildNodes(
        IReadOnlyDictionary<string, JsonElement> nodes,
        JsonElement node)
    {
        if (!node.TryGetProperty("children", out JsonElement children)
            || children.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<JsonElement>();
        }

        return children.EnumerateArray()
            .Where(childId => childId.ValueKind == JsonValueKind.String)
            .Select(childId => childId.GetString())
            .Where(nodes.ContainsKey)
            .Select(key => nodes[key]);
    }

    private static StrokeStyle ReadStroke(JsonElement node)
    {
        if (!node.TryGetProperty("stroke", out JsonElement stroke) || stroke.ValueKind != JsonValueKind.Object)
        {
            return new StrokeStyle();
        }

        return new StrokeStyle
        {
            Visible = Bool(stroke, "visible"),
            Width = Dbl(stroke, "width"),
            Colour = ReadColour(stroke),
            Rough = ReadRough(stroke),
        };
    }

    private static StrokeStyle ReadGridStroke(JsonElement gridNode)
    {
        if (gridNode.TryGetProperty("cleanOptions", out JsonElement clean) && clean.ValueKind == JsonValueKind.Object)
        {
            return new StrokeStyle
            {
                Visible = true,
                Width = Dbl(clean, "width"),
                Colour = ReadColour(clean),
            };
        }

        return new StrokeStyle();
    }

    private static FillStyle ReadFill(JsonElement node)
    {
        if (!node.TryGetProperty("fill", out JsonElement fill) || fill.ValueKind != JsonValueKind.Object)
        {
            return new FillStyle();
        }

        return new FillStyle
        {
            Visible = Bool(fill, "visible"),
            Colour = ReadColour(fill),
        };
    }

    private static Rgba ReadColour(JsonElement owner)
    {
        if (!owner.TryGetProperty("colour", out JsonElement colour) || colour.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        int packed = 0;
        if (colour.TryGetProperty("colour", out JsonElement packedElement)
            && packedElement.ValueKind == JsonValueKind.Number)
        {
            packed = packedElement.TryGetInt32(out int value) ? value : (int)packedElement.GetDouble();
        }

        double alpha = 1;
        if (colour.TryGetProperty("alpha", out JsonElement alphaElement)
            && alphaElement.ValueKind == JsonValueKind.Number)
        {
            alpha = alphaElement.GetDouble();
        }

        return Rgba.FromPacked(packed, alpha);
    }

    private static RoughLevel ReadRough(JsonElement owner)
    {
        if (owner.TryGetProperty("roughOptions", out JsonElement rough) && rough.ValueKind == JsonValueKind.String)
        {
            return rough.GetString() switch
            {
                "low" => RoughLevel.Low,
                "high" => RoughLevel.High,
                _ => RoughLevel.None,
            };
        }

        return RoughLevel.None;
    }

    private static MapBounds BoundsOf(VectorGeometry geometry) =>
        OverallBounds(new[] { geometry });

    private static MapBounds OverallBounds(IEnumerable<VectorGeometry> geometries)
    {
        bool any = false;
        double minX = 0;
        double minY = 0;
        double maxX = 0;
        double maxY = 0;

        foreach (VectorGeometry geometry in geometries)
        {
            foreach (MapPoint point in AllPoints(geometry))
            {
                if (!any)
                {
                    minX = maxX = point.X;
                    minY = maxY = point.Y;
                    any = true;
                    continue;
                }

                if (point.X < minX) minX = point.X;
                if (point.X > maxX) maxX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.Y > maxY) maxY = point.Y;
            }
        }

        return any ? new MapBounds(minX, minY, maxX, maxY) : default;
    }

    private static IEnumerable<MapPoint> AllPoints(VectorGeometry geometry)
    {
        foreach (MapPolygon polygon in geometry.Polygons)
        {
            foreach (PolygonRing ring in polygon.Rings)
            {
                foreach (MapPoint point in ring.Vertices)
                {
                    yield return point;
                }
            }
        }

        foreach (Polyline polyline in geometry.Polylines)
        {
            foreach (MapPoint point in polyline.Points)
            {
                yield return point;
            }
        }
    }

    private static string TypeOf(JsonElement node) => Str(node, "type");

    // These read from an arbitrary node; guard ValueKind first because TryGetProperty throws on
    // a non-object element (a malformed .ds can put a bare value where a node object is expected).
    private static string Str(JsonElement node, string property, string fallback = "")
    {
        return node.ValueKind == JsonValueKind.Object
            && node.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static bool Bool(JsonElement node, string property, bool fallback = false)
    {
        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(property, out JsonElement value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback,
        };
    }

    private static double Dbl(JsonElement node, string property, double fallback = 0)
    {
        return node.ValueKind == JsonValueKind.Object
            && node.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : fallback;
    }

    private static Result<VectorMap> Invalid(string message) =>
        Result.WithMessages<VectorMap>(ValidationMessage.Error(message, "ds"));

    private static string MapIdFromPath(string filePath) => Path.GetFileNameWithoutExtension(filePath);

    private static string AdventureFromPath(string filePath)
    {
        string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        return directory.Length == 0 ? string.Empty : Path.GetFileName(directory);
    }
}
