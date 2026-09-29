using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Core.Session;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// Renders a <see cref="VectorMap"/> to a self-contained SVG string in the map's own world
/// units (the viewBox is the geometry bounds). Draws the book-style stack bottom-to-top:
/// floor fill, an approximate inner-wall shadow, the grid (clipped to the floor), the wall
/// strokes, then the door/stair object pass. The parametric effect layers (buffer shading,
/// hatching) are intentionally not drawn yet — their exact render parameters are deferred to
/// a later pass; the layers are still present in the model.
/// Stateless, so it is safe to use as a singleton.
/// </summary>
public static class VectorMapSvgRenderer
{
    // The DS SHADOW layer's parameters are not yet modelled; these approximate the classic
    // template (offset the wall outlines onto the floor and tint them grey).
    private const double ShadowOffsetX = 12;
    private const double ShadowOffsetY = 9;
    private const string ShadowColour = "#8C867D";
    private const string DoorFill = "#FFFFFF";
    private const string ObjectStroke = "#000000";
    private const double ObjectStrokeWidth = 2;

    // How solid a still-concealed object is drawn on the DM's own map: ghosted enough to read as
    // "the players cannot see this yet", solid enough for the DM to spot and click it.
    private const double ConcealedOpacity = 0.3;

    // The two DM-only map annotations. Both must read over parchment floor, the grey wall band and
    // the blue fog tint at once, so each is a saturated hue none of those three uses.
    private const string PingColour = "#e0483c";
    private const string MeasureColour = "#f5c542";

    // The Room Editor's outline in progress, in the orange its rectangle preview (dt-editor.js) uses.
    private const string DraftColour = "#e67e22";

    // How far an open door leaf swings from the wall — about a third of a full 90° swing, so it reads
    // clearly as ajar without looking like a second door standing in the perpendicular wall.
    private static readonly double OpenSwingRadians = Math.PI / 6;

    // A defensive ceiling: a malformed .ds with a tiny cell size relative to its extent could
    // otherwise ask for millions of grid lines and exhaust memory on the render thread.
    private const int MaxGridLinesPerAxis = 2000;

    /// <summary>Renders the map to an SVG document string.</summary>
    /// <param name="map">The map to render.</param>
    /// <param name="options">Layer toggles and padding.</param>
    /// <returns>A complete <c>&lt;svg&gt;</c> element.</returns>
    public static string Render(VectorMap map, VectorMapRenderOptions options)
    {
        MapBounds bounds = map.Bounds;
        double pad = options.Padding;
        double minX = bounds.MinX - pad;
        double minY = bounds.MinY - pad;
        double width = bounds.Width + (pad * 2);
        double height = bounds.Height + (pad * 2);

        // The player view overrides the viewBox to show only the DM-chosen region; masks and grid
        // still cover the full padded bounds below.
        MapBounds view = options.ViewBox.Width > 0 && options.ViewBox.Height > 0
            ? options.ViewBox
            : new MapBounds(minX, minY, minX + width, minY + height);

        var svg = new StringBuilder(64 * 1024);
        svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"")
            .Append(N(view.MinX)).Append(' ').Append(N(view.MinY)).Append(' ')
            .Append(N(view.Width)).Append(' ').Append(N(view.Height))
            .Append("\" preserveAspectRatio=\"xMidYMid meet\" class=\"vector-map\">");

        AppendMapContent(svg, map, options, minX, minY, width, height);

        // Fog covers the map content but sits below the editor overlay (so the DM keeps drawing over
        // it) and below the revealed markers it draws itself (so they read on the player projector).
        if (options.Fog.Enabled)
        {
            AppendFog(svg, options.Fog, minX, minY, width, height);
        }

        if (!options.Overlay.IsEmpty)
        {
            AppendEditorOverlay(svg, options.Overlay);
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    // Draws the book-style map itself (defs, exterior shading, floor, grid, walls, objects/decoration)
    // in world coordinates, without the &lt;svg&gt; wrapper. Shared by the DM view (Render) and the
    // player projector (RenderRevealed wraps it in a reveal mask), so both draw identical geometry.
    private static void AppendMapContent(
        StringBuilder svg, VectorMap map, VectorMapRenderOptions options,
        double minX, double minY, double width, double height)
    {
        TryFindLayer(map.Layers, LayerKind.Floor, out MapLayer floor);
        TryFindLayer(map.Layers, LayerKind.Walls, out MapLayer walls);

        // Any FloorHoles are appended as extra subpaths so the even-odd fill/clip punches them out of
        // the floor — whatever covers a hole (a secret door) then renders as wall (hatch + halo, no
        // parchment, no grid), exactly like the surrounding wall.
        string floorPath = PolygonPathData(floor.Geometry) + RectsPathData(options.FloorHoles);
        string wallPath = PolygonPathData(walls.Geometry);

        // Open doors punch the wall so there is a real gap (no wall drawn where an open door is), on the
        // DM view exactly as on the player reveal. Closed doors keep their wall.
        string doorGap = OpenDoorHoles(map, options.Doors, StrokeWidth(walls));

        // Floor clip: everything below the walls is confined to the floor interior. Decoration
        // sprites are defined once here and referenced by <use> at each placement.
        svg.Append("<defs><clipPath id=\"dt-floor\" clipPathUnits=\"userSpaceOnUse\"><path d=\"")
            .Append(floorPath).Append("\" clip-rule=\"evenodd\"/></clipPath>");
        bool drawShading = options.ShowWallShading && floorPath.Length > 0;
        if (drawShading)
        {
            AppendWallShadingDefs(svg, floorPath, minX, minY, width, height);
        }

        if (options.ShowObjects)
        {
            AppendSpriteDefs(svg, map.Sprites);
        }

        if (doorGap.Length > 0)
        {
            AppendMaskOpen(svg, "dt-doorgap", minX, minY, width, height, whiteBase: true);
            svg.Append("<path d=\"").Append(doorGap).Append("\" fill=\"black\"/></mask>");
        }

        svg.Append("</defs>");

        // Book-style exterior: a soft grey halo and a diagonal hatch band hugging the outside of
        // every wall (masked to the region outside the floor), painted before the floor fill.
        if (drawShading)
        {
            AppendOutsideEffects(svg, floorPath, minX, minY, width, height);
        }

        if (floorPath.Length > 0 && floor.Visible && floor.Fill.Visible)
        {
            AppendPath(svg, floorPath, floor.Fill.Colour.ToHex(), fillRuleEvenOdd: true,
                fillOpacity: Opacity(floor.Fill.Colour.A, floor.Alpha));
        }

        bool hasClippedLayers = (options.ShowShadow || options.ShowGrid) && floorPath.Length > 0;
        if (hasClippedLayers)
        {
            svg.Append("<g clip-path=\"url(#dt-floor)\">");

            if (options.ShowShadow && wallPath.Length > 0)
            {
                double shadowWidth = walls.Stroke.Width > 0 ? walls.Stroke.Width : ObjectStrokeWidth;
                svg.Append("<path d=\"").Append(wallPath)
                    .Append("\" fill=\"none\" stroke=\"").Append(ShadowColour)
                    .Append("\" stroke-width=\"").Append(N(shadowWidth))
                    .Append("\" stroke-opacity=\"0.45\" stroke-linejoin=\"round\" transform=\"translate(")
                    .Append(N(ShadowOffsetX)).Append(',').Append(N(ShadowOffsetY)).Append(")\"/>");
            }

            if (options.ShowGrid)
            {
                AppendGrid(svg, map.Grid, map.Bounds);
            }

            svg.Append("</g>");
        }

        bool drawWalls = walls.Visible && walls.Stroke.Visible;
        double wallOpacity = Opacity(walls.Stroke.Colour.A, walls.Alpha);
        string wallLines = PolylinePathData(walls.Geometry);
        bool maskWalls = doorGap.Length > 0 && drawWalls;
        if (maskWalls)
        {
            svg.Append("<g mask=\"url(#dt-doorgap)\">");
        }

        if (drawWalls && wallPath.Length > 0)
        {
            AppendStroke(svg, wallPath, walls.Stroke.Colour.ToHex(), StrokeWidth(walls), wallOpacity);
        }

        if (drawWalls && wallLines.Length > 0)
        {
            AppendStroke(svg, wallLines, walls.Stroke.Colour.ToHex(), StrokeWidth(walls), wallOpacity);
        }

        if (maskWalls)
        {
            svg.Append("</g>");
        }

        if (options.ShowObjects)
        {
            // This render only ever feeds a DM-side view, so a still-concealed object is ghosted
            // rather than omitted — the DM keeps seeing (and can click) what the players cannot.
            AppendDecorations(svg, map, options.Conceal, omitHidden: false);
            AppendObjects(svg, map, options, walls.Stroke.Colour.ToHex(), StrokeWidth(walls));
        }
    }

    private static double StrokeWidth(MapLayer layer) =>
        layer.Stroke.Width > 0 ? layer.Stroke.Width : 1;

    private static void AppendWallShadingDefs(
        StringBuilder svg, string floorPath, double minX, double minY, double width, double height)
    {
        // 45-degree diagonal hatch, used as a FILL (a wide patterned stroke on the complex floor
        // path renders unreliably, so the hatch band is a pattern-filled rect masked to the walls).
        AppendHatchPattern(svg);

        // Wall band: a wide solid stroke of the outline (both sides), with the floor interior
        // punched out, leaves a band hugging the OUTSIDE of every wall.
        svg.Append("<mask id=\"dt-band\" maskUnits=\"userSpaceOnUse\" x=\"").Append(N(minX))
            .Append("\" y=\"").Append(N(minY)).Append("\" width=\"").Append(N(width))
            .Append("\" height=\"").Append(N(height)).Append("\"><path d=\"").Append(floorPath)
            .Append("\" fill=\"none\" stroke=\"white\" stroke-width=\"56\" stroke-linejoin=\"round\"/><path d=\"")
            .Append(floorPath).Append("\" fill=\"black\" fill-rule=\"evenodd\"/></mask>");

        // Full exterior mask for the soft halo.
        svg.Append("<mask id=\"dt-outside\" maskUnits=\"userSpaceOnUse\" x=\"").Append(N(minX))
            .Append("\" y=\"").Append(N(minY)).Append("\" width=\"").Append(N(width))
            .Append("\" height=\"").Append(N(height)).Append("\"><rect x=\"").Append(N(minX))
            .Append("\" y=\"").Append(N(minY)).Append("\" width=\"").Append(N(width))
            .Append("\" height=\"").Append(N(height)).Append("\" fill=\"white\"/><path d=\"")
            .Append(floorPath).Append("\" fill=\"black\" fill-rule=\"evenodd\"/></mask>");

        svg.Append("<filter id=\"dt-soft\" x=\"-8%\" y=\"-8%\" width=\"116%\" height=\"116%\">")
            .Append("<feGaussianBlur stdDeviation=\"6\"/></filter>");
    }

    private static void AppendOutsideEffects(
        StringBuilder svg, string floorPath, double minX, double minY, double width, double height)
    {
        // Soft grey halo fading outward (buffer shading), masked to the exterior.
        svg.Append("<g mask=\"url(#dt-outside)\" stroke-linejoin=\"round\">");
        svg.Append("<path d=\"").Append(floorPath)
            .Append("\" fill=\"none\" stroke=\"#C5B9AC\" stroke-width=\"44\" filter=\"url(#dt-soft)\"/>");
        svg.Append("<path d=\"").Append(floorPath)
            .Append("\" fill=\"none\" stroke=\"#8C867D\" stroke-width=\"20\" stroke-opacity=\"0.5\" filter=\"url(#dt-soft)\"/>");
        svg.Append("</g>");

        // The diagonal hatch band: a pattern-filled rectangle masked to the wall band.
        svg.Append("<rect x=\"").Append(N(minX)).Append("\" y=\"").Append(N(minY))
            .Append("\" width=\"").Append(N(width)).Append("\" height=\"").Append(N(height))
            .Append("\" fill=\"url(#dt-hatch)\" mask=\"url(#dt-band)\"/>");
    }

    // The book-style 45-degree diagonal hatch, referenced as a fill by the wall band on both the DM
    // view (AppendWallShadingDefs) and the player reveal (RenderRevealed).
    private static void AppendHatchPattern(StringBuilder svg) =>
        svg.Append("<pattern id=\"dt-hatch\" patternUnits=\"userSpaceOnUse\" width=\"7\" height=\"7\" ")
            .Append("patternTransform=\"rotate(45)\">")
            .Append("<path d=\"M0 0 L0 7\" stroke=\"#2b2b2b\" stroke-width=\"1.5\"/></pattern>");

    private static void AppendGrid(StringBuilder svg, Grid grid, MapBounds bounds)
    {
        if (grid.CellSizePx <= 0 || grid.Cols > MaxGridLinesPerAxis || grid.Rows > MaxGridLinesPerAxis)
        {
            return;
        }

        double width = grid.CellSizePx;
        string colour = "#555555";
        svg.Append("<g stroke=\"").Append(colour).Append("\" stroke-width=\"1\">");

        for (int col = 0; col <= grid.Cols; col++)
        {
            double x = grid.OriginX + (col * width);
            svg.Append("<line x1=\"").Append(N(x)).Append("\" y1=\"").Append(N(bounds.MinY))
                .Append("\" x2=\"").Append(N(x)).Append("\" y2=\"").Append(N(bounds.MaxY)).Append("\"/>");
        }

        for (int row = 0; row <= grid.Rows; row++)
        {
            double y = grid.OriginY + (row * width);
            svg.Append("<line x1=\"").Append(N(bounds.MinX)).Append("\" y1=\"").Append(N(y))
                .Append("\" x2=\"").Append(N(bounds.MaxX)).Append("\" y2=\"").Append(N(y)).Append("\"/>");
        }

        svg.Append("</g>");
    }

    private static void AppendSpriteDefs(StringBuilder svg, IReadOnlyList<SpriteImage> sprites)
    {
        foreach (SpriteImage sprite in sprites)
        {
            if (sprite.DataUri.Length == 0 || sprite.Width <= 0 || sprite.Height <= 0)
            {
                continue;
            }

            // Top-left anchored: Dungeon Scrawl's placement transform maps the asset's local
            // origin (its top-left corner), not its centre.
            svg.Append("<image id=\"sp-").Append(AttributeEscape(sprite.AssetId))
                .Append("\" x=\"0\" y=\"0\" width=\"").Append(N(sprite.Width))
                .Append("\" height=\"").Append(N(sprite.Height))
                .Append("\" href=\"").Append(AttributeEscape(sprite.DataUri)).Append("\"/>");
        }
    }

    // Draws the placed decoration sprites. Each placement is wrapped in an id-tagged group (like the
    // door/stair pass) so the Room Editor and the DM's Conceal mode can hit-test an individual
    // statue or pit. A concealed one is either ghosted (DM views) or dropped entirely
    // (<paramref name="omitHidden"/>, the player projector).
    private static void AppendDecorations(
        StringBuilder svg, VectorMap map, ConcealState conceal, bool omitHidden)
    {
        if (map.Decorations.Count == 0)
        {
            return;
        }

        svg.Append("<g class=\"decorations\">");
        foreach (MapDecoration decoration in map.Decorations)
        {
            bool hidden = !conceal.IsEmpty && conceal.IsHidden(decoration.Id);
            if (hidden && omitHidden)
            {
                continue;
            }

            OpenObjectGroup(svg, "decoration", decoration.Id);

            AffineTransform t = decoration.Transform;
            double alpha = hidden ? decoration.Alpha * ConcealedOpacity : decoration.Alpha;
            svg.Append("<use href=\"#sp-").Append(AttributeEscape(decoration.AssetId))
                .Append("\" transform=\"matrix(")
                .Append(N(t.A)).Append(' ').Append(N(t.B)).Append(' ').Append(N(t.C)).Append(' ')
                .Append(N(t.D)).Append(' ').Append(N(t.E)).Append(' ').Append(N(t.F)).Append(")\"");
            if (alpha < 1)
            {
                svg.Append(" opacity=\"").Append(N(alpha)).Append('"');
            }

            svg.Append("/></g>");
        }

        svg.Append("</g>");
    }

    private static void AppendObjects(
        StringBuilder svg, VectorMap map, VectorMapRenderOptions options, string wallColour, double wallWidth)
    {
        svg.Append("<g stroke-linejoin=\"round\">");

        foreach (DoorFeature door in map.Doors)
        {
            // A secret door stops being secret once the DM discovers it — it then renders as a normal
            // door on every view, including the player's.
            bool secret = options.Doors.IsHiddenSecret(door.Id);

            // Player view: a secret door renders as an anonymous wall segment — no door glyph, no
            // "S", and crucially no data-object-* hook — so it is indistinguishable from the wall.
            if (secret && !options.MarkSecretDoors)
            {
                AppendDoorAsWall(svg, door, wallColour, wallWidth);
                continue;
            }

            // Wrap each object in an id-tagged group so the editor / visibility engine can
            // hit-test and toggle an individual door or stair without touching the geometry.
            OpenObjectGroup(svg, "door", door.Id);

            if (secret)
            {
                // DM view: the door reads as continuous wall, marked with the book-style secret "S".
                AppendDoorAsWall(svg, door, wallColour, wallWidth);
                AppendSecretMark(svg, door);
            }
            else
            {
                AppendDoor(svg, door,
                    open: options.Doors.OpenDoorIds.Contains(door.Id),
                    dbl: options.Doors.DoubleDoorIds.Contains(door.Id));
            }

            svg.Append("</g>");
        }

        foreach (StairFeature stair in map.Stairs)
        {
            OpenObjectGroup(svg, "stairs", stair.Id);

            string steps = PolylinePathData(stair.Geometry);
            if (steps.Length > 0)
            {
                // A concealed staircase stays drawn and clickable for the DM, only ghosted; the
                // player render (RenderRevealed) leaves it out entirely instead.
                bool hidden = !options.Conceal.IsEmpty && options.Conceal.IsHidden(stair.Id);
                AppendStroke(svg, steps, ObjectStroke, ObjectStrokeWidth, hidden ? ConcealedOpacity : 1);
            }

            svg.Append("</g>");
        }

        svg.Append("</g>");
    }

    private static void AppendDoorAsWall(StringBuilder svg, DoorFeature door, string wallColour, double wallWidth)
    {
        string segment = DoorHealSegment(door);
        if (segment.Length == 0)
        {
            return;
        }

        // Heal the wall across the opening with a stroked segment the thickness of the wall itself,
        // emitted exactly like the wall polylines (a <path> with fill:none stroke:wallColour) — NOT
        // a filled <rect>. On the player view a distinctively-shaped element would let anyone reading
        // the SVG source enumerate secret doors (walls/doors are <path>s, so a lone wall-coloured
        // <rect> pinpoints each one); a wall-style stroke is indistinguishable from the wall itself.
        // Round caps extend half the stroke width past each endpoint, closing the opening flush with
        // the neighbouring wall.
        double thickness = wallWidth > 0 ? wallWidth : ObjectStrokeWidth;
        AppendStroke(svg, segment, wallColour, thickness);
    }

    // The wall-healing segment for a door: a straight line along the wall it sits in, spanning the opening.
    // Shared by the DM view (AppendDoorAsWall) and the player reveal (which strokes it into the wall path),
    // so a secret door is healed the same way in both and cannot look different from an ordinary wall.
    private static string DoorHealSegment(DoorFeature door)
    {
        MapBounds bounds = door.Bounds;
        if (bounds.Width <= 0 && bounds.Height <= 0)
        {
            return string.Empty;
        }

        double centreX = (bounds.MinX + bounds.MaxX) / 2;
        double centreY = (bounds.MinY + bounds.MaxY) / 2;
        bool vertical = door.Orientation == DoorOrientation.Vertical
            || (door.Orientation == DoorOrientation.None && bounds.Height >= bounds.Width);

        return vertical
            ? "M" + N(centreX) + ' ' + N(bounds.MinY) + 'L' + N(centreX) + ' ' + N(bounds.MaxY)
            : "M" + N(bounds.MinX) + ' ' + N(centreY) + 'L' + N(bounds.MaxX) + ' ' + N(centreY);
    }

    private static void AppendSecretMark(StringBuilder svg, DoorFeature door)
    {
        MapBounds bounds = door.Bounds;
        double span = Math.Max(bounds.Width, bounds.Height);
        if (span <= 0)
        {
            return;
        }

        // The book convention: a black "S" straddling the wall, laid flush along it. A door in a
        // horizontal (E-W) wall — one the party walks through going north/south — gets the "S" turned
        // sideways to lie along that wall; a door in a vertical (N-S) wall keeps it upright. A
        // parchment halo keeps it legible over the wall band.
        double centreX = (bounds.MinX + bounds.MaxX) / 2;
        double centreY = (bounds.MinY + bounds.MaxY) / 2;
        double fontSize = span * 1.05;

        bool verticalWall = door.Orientation == DoorOrientation.Vertical
            || (door.Orientation == DoorOrientation.None && bounds.Height >= bounds.Width);

        svg.Append("<text x=\"").Append(N(centreX)).Append("\" y=\"").Append(N(centreY))
            .Append("\" font-size=\"").Append(N(fontSize))
            .Append("\" text-anchor=\"middle\" dominant-baseline=\"central\" font-family=\"Georgia, 'Times New Roman', serif\" ")
            .Append("font-weight=\"bold\" fill=\"#1b1b1b\" paint-order=\"stroke\" stroke=\"#f4f1ea\" stroke-width=\"")
            .Append(N(fontSize * 0.12)).Append('"');
        if (!verticalWall)
        {
            svg.Append(" transform=\"rotate(90 ").Append(N(centreX)).Append(' ').Append(N(centreY)).Append(")\"");
        }

        svg.Append(">S</text>");
    }

    // Draws a door glyph in one of four presentations. A closed single door keeps the source geometry
    // (leaf slab + jamb stubs), so the common case is identical to before; the open and double states
    // are synthesized from the door's opening (bounds + orientation), since the source file carries
    // only the single closed shape. Callers draw a still-secret door as wall instead of calling this.
    private static void AppendDoor(StringBuilder svg, DoorFeature door, bool open, bool dbl)
    {
        if (!open && !dbl && AppendClosedGeometry(svg, door))
        {
            return;
        }

        MapBounds b = door.Bounds;
        double span = Math.Max(b.Width, b.Height);
        if (span <= 0)
        {
            AppendClosedGeometry(svg, door);
            return;
        }

        double cx = (b.MinX + b.MaxX) / 2;
        double cy = (b.MinY + b.MaxY) / 2;
        bool vertical = door.Orientation == DoorOrientation.Vertical
            || (door.Orientation == DoorOrientation.None && b.Height >= b.Width);

        // Along = the wall/opening direction; Perp = into the room (a leaf swings this way when open).
        double ax = vertical ? 0 : 1, ay = vertical ? 1 : 0;
        double px = vertical ? 1 : 0, py = vertical ? 0 : 1;
        double half = span / 2;
        double thickness = Math.Max(span * 0.16, ObjectStrokeWidth);
        double e0x = cx - (ax * half), e0y = cy - (ay * half);
        double e1x = cx + (ax * half), e1y = cy + (ay * half);

        if (!open)
        {
            // Closed double: two leaves meeting at the centre, along the wall, with a seam line.
            AppendLeaf(svg, e0x, e0y, ax, ay, half, thickness);
            AppendLeaf(svg, e1x, e1y, -ax, -ay, half, thickness);
            AppendStroke(svg, "M" + N(cx - (px * thickness)) + ' ' + N(cy - (py * thickness))
                + 'L' + N(cx + (px * thickness)) + ' ' + N(cy + (py * thickness)), ObjectStroke, ObjectStrokeWidth);
            return;
        }

        // Open: swing each leaf only about a third of the way (OpenSwingRadians) — clearly ajar, but not
        // the full perpendicular leaf that reads like a second door standing in the side wall. The leaf
        // rotates from lying along the wall toward the room (perp), hinged at its jamb.
        double cos = Math.Cos(OpenSwingRadians);
        double sin = Math.Sin(OpenSwingRadians);
        if (dbl)
        {
            AppendLeaf(svg, e0x, e0y, (ax * cos) + (px * sin), (ay * cos) + (py * sin), half, thickness);
            AppendLeaf(svg, e1x, e1y, (-ax * cos) + (px * sin), (-ay * cos) + (py * sin), half, thickness);
        }
        else
        {
            AppendLeaf(svg, e0x, e0y, (ax * cos) + (px * sin), (ay * cos) + (py * sin), span, thickness);
        }
    }

    // Draws the source door geometry (leaf slab + jamb stubs). Returns false when the door carries no
    // geometry (synthetic/test maps), so the caller can synthesize a leaf instead.
    private static bool AppendClosedGeometry(StringBuilder svg, DoorFeature door)
    {
        string body = PolygonPathData(door.Geometry);
        string jambs = PolylinePathData(door.Geometry);
        if (body.Length == 0 && jambs.Length == 0)
        {
            return false;
        }

        if (body.Length > 0)
        {
            svg.Append("<path d=\"").Append(body).Append("\" fill=\"").Append(DoorFill)
                .Append("\" stroke=\"").Append(ObjectStroke).Append("\" stroke-width=\"")
                .Append(N(ObjectStrokeWidth)).Append("\" stroke-linejoin=\"round\"/>");
        }

        if (jambs.Length > 0)
        {
            AppendStroke(svg, jambs, ObjectStroke, ObjectStrokeWidth);
        }

        return true;
    }

    // A door leaf as a thin filled rectangle: from (hx,hy) along the unit direction (dux,duy) for len,
    // with thickness t laid perpendicular to that direction. Filled like a door slab so open leaves read
    // the same as closed ones.
    private static void AppendLeaf(
        StringBuilder svg, double hx, double hy, double dux, double duy, double len, double t)
    {
        double ex = hx + (dux * len), ey = hy + (duy * len);
        double ox = -duy * (t / 2), oy = dux * (t / 2);
        string d = "M" + N(hx + ox) + ' ' + N(hy + oy)
            + 'L' + N(ex + ox) + ' ' + N(ey + oy)
            + 'L' + N(ex - ox) + ' ' + N(ey - oy)
            + 'L' + N(hx - ox) + ' ' + N(hy - oy) + 'Z';
        svg.Append("<path d=\"").Append(d).Append("\" fill=\"").Append(DoorFill)
            .Append("\" stroke=\"").Append(ObjectStroke).Append("\" stroke-width=\"")
            .Append(N(ObjectStrokeWidth)).Append("\" stroke-linejoin=\"round\"/>");
    }

    private static void OpenObjectGroup(StringBuilder svg, string kind, string id)
    {
        svg.Append("<g data-object-kind=\"").Append(kind)
            .Append("\" data-object-id=\"").Append(AttributeEscape(id)).Append("\">");
    }

    private static string AttributeEscape(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    private static void AppendPath(
        StringBuilder svg, string pathData, string fill, bool fillRuleEvenOdd, double fillOpacity)
    {
        svg.Append("<path d=\"").Append(pathData).Append("\" fill=\"").Append(fill).Append('"');
        if (fillRuleEvenOdd)
        {
            svg.Append(" fill-rule=\"evenodd\"");
        }

        if (fillOpacity < 1)
        {
            svg.Append(" fill-opacity=\"").Append(N(fillOpacity)).Append('"');
        }

        svg.Append("/>");
    }

    private static void AppendStroke(StringBuilder svg, string pathData, string colour, double width, double opacity = 1)
    {
        svg.Append("<path d=\"").Append(pathData)
            .Append("\" fill=\"none\" stroke=\"").Append(colour)
            .Append("\" stroke-width=\"").Append(N(width)).Append('"');
        if (opacity < 1)
        {
            svg.Append(" stroke-opacity=\"").Append(N(opacity)).Append('"');
        }

        svg.Append(" stroke-linejoin=\"round\" stroke-linecap=\"round\"/>");
    }

    private static double Opacity(byte channelAlpha, double layerAlpha) =>
        (channelAlpha / 255.0) * layerAlpha;

    private static string PolygonPathData(VectorGeometry geometry)
    {
        var d = new StringBuilder();
        foreach (MapPolygon polygon in geometry.Polygons)
        {
            foreach (PolygonRing ring in polygon.Rings)
            {
                AppendRing(d, ring.Vertices, close: true);
            }
        }

        return d.ToString();
    }

    private static string PolylinePathData(VectorGeometry geometry)
    {
        var d = new StringBuilder();
        foreach (Polyline polyline in geometry.Polylines)
        {
            AppendRing(d, polyline.Points, close: false);
        }

        return d.ToString();
    }

    // Rectangles as closed subpaths, for punching floor holes or building a union mask path (drawn as
    // one path so overlapping rects union under non-zero fill instead of compounding their opacity).
    private static string RectsPathData(IReadOnlyList<MapBounds> rects)
    {
        var d = new StringBuilder();
        foreach (MapBounds b in rects)
        {
            if (!IsFinite(b))
            {
                continue;
            }

            d.Append('M').Append(N(b.MinX)).Append(' ').Append(N(b.MinY))
                .Append('h').Append(N(b.Width)).Append('v').Append(N(b.Height))
                .Append('h').Append(N(-b.Width)).Append('Z');
        }

        return d.ToString();
    }

    private static void AppendRing(StringBuilder d, IReadOnlyList<MapPoint> points, bool close)
    {
        if (points.Count == 0)
        {
            return;
        }

        d.Append('M').Append(N(points[0].X)).Append(' ').Append(N(points[0].Y));
        for (int i = 1; i < points.Count; i++)
        {
            d.Append('L').Append(N(points[i].X)).Append(' ').Append(N(points[i].Y));
        }

        if (close)
        {
            d.Append('Z');
        }
    }

    private static bool TryFindLayer(IReadOnlyList<MapLayer> layers, LayerKind kind, out MapLayer found)
    {
        foreach (MapLayer layer in layers)
        {
            if (layer.Kind == kind)
            {
                found = layer;
                return true;
            }

            if (layer.Children.Count > 0 && TryFindLayer(layer.Children, kind, out found))
            {
                return true;
            }
        }

        found = new MapLayer();
        return false;
    }

    // ---- Fog of war -----------------------------------------------------------------------------
    // An opaque (player) or translucent (DM) sheet over the whole map, masked so the revealed
    // regions and painted cells punch transparent holes. Revealed markers are drawn on top of the
    // sheet so they stay visible on the player projector.

    // Flat sheet (DM view): a translucent rect with the revealed shapes punched out, so the DM sees
    // the whole map tinted where the players cannot yet see. The player projector does NOT use this —
    // it gets a culled render of only the revealed geometry (see RenderRevealed).
    private static void AppendFog(
        StringBuilder svg, FogOverlay fog, double minX, double minY, double width, double height)
    {
        // Reveal mask: white (fog opaque) everywhere, black (fog transparent) over revealed shapes.
        AppendMaskOpen(svg, "dt-reveal", minX, minY, width, height, whiteBase: true);
        AppendRevealShapes(svg, fog, "fill=\"black\"");
        svg.Append("</mask>");

        AppendFullRect(svg, minX, minY, width, height, AttributeEscape(fog.FogColour), fog.FogOpacity, "dt-reveal");

        double unit = fog.UnitSize > 0 ? fog.UnitSize : 1;
        foreach (FeatureMarker marker in fog.RevealedMarkers)
        {
            AppendMarkerGlyph(svg, marker, unit, selected: false);
        }
    }

    // ---- Player reveal render -------------------------------------------------------------------

    /// <summary>
    /// Renders the revealed area for the player projector as a strict tile model. The DM sends the whole
    /// map (floor, walls, doors, objects) plus the set of revealed squares; this draws every revealed
    /// square exactly as the map describes — its REAL floor clipped to the tile, plus the grid — then
    /// reaches up to two squares outward THROUGH OPEN edges only (walls and still-hidden doors stop the
    /// reach), drawing those the same way but darker with distance. The book wall band is built by
    /// DILATING the drawn floor (the map floor intersected with the drawn tiles): it follows the real
    /// floor shape inside a room, yet is cut straight at the edge of the drawn tiles, so a square beyond a
    /// wall or a hidden door contributes NOTHING — its floor is never in the drawn set and its walls are
    /// never generated. A hidden door is therefore indistinguishable from wall; a normal or DM-revealed
    /// door draws as a door. The player view runs on the DM's own machine (beamed to the table), so
    /// drawing the revealed geometry here is fine.
    /// </summary>
    /// <param name="map">The map (its grid, bounds, floor, walls, objects and decorations).</param>
    /// <param name="viewBox">The projector's viewport in world units (empty = whole map).</param>
    /// <param name="regions">Revealed room polygons.</param>
    /// <param name="cells">Revealed fog-brush cells.</param>
    /// <param name="markers">Revealed feature markers to draw on top.</param>
    /// <param name="doors">Per-door presentation state. A still-secret (undiscovered) door is drawn as
    /// wall and blocks the peek; a discovered door draws as a normal door (open or closed / double).</param>
    /// <param name="conceal">Which stairs and decorations are concealed and which the DM has revealed.
    /// A still-concealed object is omitted from this render entirely, so it never reaches the
    /// projector even on a fully revealed tile.</param>
    /// <returns>A complete <c>&lt;svg&gt;</c> element for the revealed area.</returns>
    public static string RenderRevealed(
        VectorMap map, MapBounds viewBox,
        IReadOnlyList<IReadOnlyList<MapPoint>> regions,
        IReadOnlyList<MapBounds> cells,
        IReadOnlyList<FeatureMarker> markers,
        DoorRenderState doors,
        ConcealState conceal)
    {
        Grid grid = map.Grid;
        MapBounds mapBounds = map.Bounds;
        const double pad = 24;
        double minX = mapBounds.MinX - pad;
        double minY = mapBounds.MinY - pad;
        double width = mapBounds.Width + (pad * 2);
        double height = mapBounds.Height + (pad * 2);
        MapBounds view = viewBox.Width > 0 && viewBox.Height > 0
            ? viewBox
            : new MapBounds(minX, minY, minX + width, minY + height);

        double c = grid.CellSizePx;
        if (c <= 0)
        {
            c = mapBounds.Width > 0 ? mapBounds.Width / 40 : 1;
        }

        double hatchR = 0.78 * c;   // wall band reach outside the drawn floor (matches the DM outer hatch)
        double haloR = 0.7 * c;     // extra filter-region margin for the glow blur
        DoorRenderState doorState = doors ?? DoorRenderState.None;
        ConcealState concealState = conceal ?? ConcealState.None;

        // Still-secret (undiscovered) doors read as wall AND block the peek/vision, exactly as before; a
        // DISCOVERED secret door drops out of this set, so it draws as a normal door and no longer blocks.
        var hiddenSecrets = new HashSet<string>(
            doorState.SecretDoorIds.Where(id => !doorState.DiscoveredDoorIds.Contains(id)), StringComparer.Ordinal);

        // Drawn tiles: the revealed squares, plus up to 2 squares out through OPEN edges — walls and
        // still-hidden doors stop the reach, but an OPEN door is a passage, so the peek runs through it
        // and shows the tiles behind it (even undiscovered ones), a step or two out.
        IReadOnlyList<MapBounds> shownCells = CorridorReveal.ShownCells(map, regions, cells);
        IReadOnlyList<(MapBounds Cell, int Distance)> dim =
            CorridorReveal.Find(map, regions, cells, steps: 2, hiddenSecrets, doorState.OpenDoorIds);

        var drawnCells = new List<MapBounds>(shownCells);
        var dimByDistance = new Dictionary<int, List<MapBounds>>();
        foreach ((MapBounds cell, int distance) in dim)
        {
            if (!IsFinite(cell))
            {
                continue;
            }

            drawnCells.Add(cell);
            if (!dimByDistance.TryGetValue(distance, out List<MapBounds> ring))
            {
                ring = new List<MapBounds>();
                dimByDistance[distance] = ring;
            }

            ring.Add(cell);
        }

        TryFindLayer(map.Layers, LayerKind.Floor, out MapLayer floor);
        TryFindLayer(map.Layers, LayerKind.Walls, out MapLayer wallsLayer);

        string mapFloorPath = PolygonPathData(floor.Geometry);
        string floorFill = floor.Fill.Visible ? floor.Fill.Colour.ToHex() : "#f4f1ea";
        string drawnClip = RectsPathData(drawnCells);

        // Walls are drawn from the WALL GEOMETRY (as the DM view does), NOT the floor edge — because a wall
        // can sit BETWEEN two floor tiles with floor continuous on both sides (the floor edge only exists at
        // a room's perimeter, so a floor-edge line cannot draw such a wall). The wall geometry already runs
        // UNBROKEN across every secret door in this map (a secret door is a hidden passage through a
        // solid-looking wall, not a gap), so drawing that geometry faithfully already renders a secret door
        // as the wall it is — no healing segment is added, and therefore no extra stroke caps poke out at
        // the door jambs. Drawn once at a uniform width and masked to the revealed neighbourhood (dt-wall),
        // so no wall changes width with its far side and no hidden room's interior walls leak in.
        double wallW = Math.Max(StrokeWidth(wallsLayer), 0.13 * c);
        string wallLinePath = PolylinePathData(wallsLayer.Geometry) + PolygonPathData(wallsLayer.Geometry);

        var distances = dimByDistance.Keys.ToList();
        distances.Sort();
        distances.Reverse(); // far ring painted first, so a nearer ring wins where they overlap

        bool hasReveal = shownCells.Count > 0;

        // The dilation/blur filters are confined to the drawn tiles' bounding box, so they stay cheap.
        (double fx, double fy, double fw, double fh) = FilterRegion(drawnCells, hatchR + haloR + c, minX, minY, width, height);

        var svg = new StringBuilder(64 * 1024);
        svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"")
            .Append(N(view.MinX)).Append(' ').Append(N(view.MinY)).Append(' ')
            .Append(N(view.Width)).Append(' ').Append(N(view.Height))
            .Append("\" preserveAspectRatio=\"xMidYMid meet\" class=\"vector-map\">");

        svg.Append("<defs>");
        AppendHatchPattern(svg);
        svg.Append("<clipPath id=\"dt-drawn\" clipPathUnits=\"userSpaceOnUse\"><path d=\"").Append(drawnClip).Append("\"/></clipPath>");
        svg.Append("<clipPath id=\"dt-mapfloor\" clipPathUnits=\"userSpaceOnUse\"><path d=\"")
            .Append(mapFloorPath).Append("\" clip-rule=\"evenodd\"/></clipPath>");

        svg.Append("<filter id=\"dt-fade\" filterUnits=\"userSpaceOnUse\" x=\"").Append(N(fx)).Append("\" y=\"").Append(N(fy))
            .Append("\" width=\"").Append(N(fw)).Append("\" height=\"").Append(N(fh))
            .Append("\"><feMorphology operator=\"dilate\" radius=\"").Append(N(0.08 * c))
            .Append("\"/><feGaussianBlur stdDeviation=\"").Append(N(0.34 * c)).Append("\"/></filter>");

        // Wall visibility: the drawn tiles dilated by HALF the wall width, so a wall stroke centred on a
        // drawn tile's edge shows at its FULL width (constant regardless of whether its far side is
        // revealed) yet the mask stops flush with that wall's OUTER edge. Dilating by the full width would
        // over-reach half a wall past the boundary and expose the stub of a perpendicular wall in the hidden
        // room beyond (a "nubbin" poking off the wall at a door); half the width hides that stub inside the
        // boundary wall's own thickness.
        svg.Append("<filter id=\"dt-walldil\" filterUnits=\"userSpaceOnUse\" x=\"").Append(N(fx)).Append("\" y=\"").Append(N(fy))
            .Append("\" width=\"").Append(N(fw)).Append("\" height=\"").Append(N(fh))
            .Append("\"><feMorphology operator=\"dilate\" radius=\"").Append(N(wallW * 0.5)).Append("\"/></filter>");
        AppendMaskOpen(svg, "dt-wall", minX, minY, width, height, whiteBase: false);
        svg.Append("<g filter=\"url(#dt-walldil)\"><path d=\"").Append(drawnClip).Append("\" fill=\"white\"/></g>");
        // Open the wall across OPEN doors so an opened door reads as a real doorway (a gap), not a leaf
        // drawn on top of a solid wall — some maps draw the wall unbroken across a door. A CLOSED door
        // keeps its wall; a still-hidden secret door is never open, so its wall stays solid and the
        // "secret door looks like wall" invariant is untouched.
        string openHoles = OpenDoorHoles(map, doorState, wallW);
        if (openHoles.Length > 0)
        {
            svg.Append("<path d=\"").Append(openHoles).Append("\" fill=\"black\"/>");
        }

        svg.Append("</mask>");

        // Inner-shadow blur: softens the wall-cast shadow into a gradient hugging the inside of the walls.
        svg.Append("<filter id=\"dt-inner\" filterUnits=\"userSpaceOnUse\" x=\"").Append(N(fx)).Append("\" y=\"").Append(N(fy))
            .Append("\" width=\"").Append(N(fw)).Append("\" height=\"").Append(N(fh))
            .Append("\"><feGaussianBlur stdDeviation=\"").Append(N(0.16 * c)).Append("\"/></filter>");
        if (map.Decorations.Count > 0)
        {
            AppendSpriteDefs(svg, map.Sprites);
        }

        svg.Append("</defs>");

        AppendFullRect(svg, minX, minY, width, height, "#05060a", 1, mask: string.Empty);
        if (!hasReveal || mapFloorPath.Length == 0)
        {
            svg.Append("</svg>");
            return svg.ToString();
        }

        // One reveal layer: its floor (clipped to the layer's tiles) + its hatch band (the layer's own drawn
        // floor DILATED, gated to the not-drawn-floor region). Wall LINES are not drawn here — they come
        // once from the real wall geometry below. A peek layer is wrapped in an opacity group, so floor and
        // hatch dim together with distance — a peek tile is exactly a revealed tile, only darker and
        // aligned. Because each layer's band comes from its OWN floor, an open frontier grows no hatch.
        void EmitLayer(IReadOnlyList<MapBounds> layerCells, string suffix, double opacity)
        {
            if (layerCells.Count == 0)
            {
                return;
            }

            string clipId = "dt-c-" + suffix;
            string bandId = "dt-b-" + suffix;
            string gateId = "dt-g-" + suffix;

            svg.Append("<clipPath id=\"").Append(clipId).Append("\" clipPathUnits=\"userSpaceOnUse\"><path d=\"")
                .Append(RectsPathData(layerCells)).Append("\"/></clipPath>");

            // Gate: the hatch is allowed anywhere that is NOT drawn floor (real floor within a drawn tile).
            // Excluding only the DRAWN floor — not all real floor — means the hidden side of a wall or secret
            // door (real floor that the DM has not revealed) still hatches, giving it the book edge; a wall
            // whose far side IS revealed sits between two drawn floors and gets no hatch, just its line.
            AppendMaskOpen(svg, gateId, minX, minY, width, height, whiteBase: true);
            svg.Append("<g clip-path=\"url(#dt-drawn)\"><path d=\"").Append(mapFloorPath).Append("\" fill=\"black\" fill-rule=\"evenodd\"/></g>");
            svg.Append("</mask>");

            // Wall band: the layer's drawn floor FADED outward (dilate + blur), so the hatch dims with
            // distance from the wall, gated to the allowed region.
            AppendMaskOpen(svg, bandId, minX, minY, width, height, whiteBase: false);
            svg.Append("<g mask=\"url(#").Append(gateId).Append(")\"><g filter=\"url(#dt-fade)\"><path d=\"")
                .Append(mapFloorPath).Append("\" clip-path=\"url(#").Append(clipId).Append(")\" fill=\"white\" fill-rule=\"evenodd\"/></g></g>");
            svg.Append("</mask>");

            svg.Append(opacity < 1 ? $"<g opacity=\"{N(opacity)}\">" : "<g>");
            // Wall body / halo: light cream, fading to black with the band.
            svg.Append("<rect x=\"").Append(N(minX)).Append("\" y=\"").Append(N(minY)).Append("\" width=\"")
                .Append(N(width)).Append("\" height=\"").Append(N(height)).Append("\" fill=\"#e6ddc6\" mask=\"url(#")
                .Append(bandId).Append(")\"/>");
            // Floor + inner shadow + grid.
            svg.Append("<g clip-path=\"url(#").Append(clipId).Append(")\">");
            AppendPath(svg, mapFloorPath, floorFill, fillRuleEvenOdd: true, fillOpacity: 1);
            // Inner wall shadow: the wall geometry as a wide, BLURRED grey stroke clipped to the floor, so a
            // soft shadow hugs the inside of every wall and fades toward the room centre (the book look).
            // Based on the wall geometry (not the floor edge) so it appears at a secret door too — keeping
            // the door indistinguishable. Clipped to the map floor so it never spills onto the hatch/black.
            svg.Append("<g clip-path=\"url(#dt-mapfloor)\"><path d=\"").Append(wallLinePath)
                .Append("\" fill=\"none\" stroke=\"").Append(ShadowColour).Append("\" stroke-width=\"").Append(N(0.72 * c))
                .Append("\" stroke-opacity=\"0.55\" stroke-linejoin=\"round\" filter=\"url(#dt-inner)\"/></g>");
            svg.Append("<g clip-path=\"url(#dt-mapfloor)\">");
            AppendGrid(svg, grid, mapBounds);
            svg.Append("</g></g>");
            // Hatch, fading with the band.
            svg.Append("<rect x=\"").Append(N(minX)).Append("\" y=\"").Append(N(minY)).Append("\" width=\"")
                .Append(N(width)).Append("\" height=\"").Append(N(height)).Append("\" fill=\"url(#dt-hatch)\" mask=\"url(#")
                .Append(bandId).Append(")\"/>");

            svg.Append("</g>");
        }

        // Peek rings first (far ring dimmest), then the revealed layer on top.
        foreach (int distance in distances)
        {
            EmitLayer(dimByDistance[distance], "d" + distance, Math.Max(0.16, 0.9 - (0.3 * distance)));
        }

        EmitLayer(shownCells, "r", 1);

        // Walls: the real wall geometry plus the healed secret doors, one uniform-width stroke over the
        // hatch, masked to the revealed neighbourhood. This is the ONLY place a wall line is drawn — a
        // secret door is just another segment in this path, indistinguishable from a wall between two tiles.
        if (wallLinePath.Length > 0)
        {
            svg.Append("<g mask=\"url(#dt-wall)\"><path d=\"").Append(wallLinePath)
                .Append("\" fill=\"none\" stroke=\"#241f18\" stroke-width=\"").Append(N(wallW))
                .Append("\" stroke-linejoin=\"round\" stroke-linecap=\"round\"/></g>");
        }

        // Objects on the revealed tiles: decorations/columns, stairs, and NON-hidden doors as doors (a
        // hidden door is drawn as nothing — the healed wall stroke above already covers it). A
        // concealed decoration or staircase is dropped outright, so a revealed room shows the room
        // without it — exactly as if it were not on the map at all.
        svg.Append("<g clip-path=\"url(#dt-drawn)\">");
        if (map.Decorations.Count > 0)
        {
            AppendDecorations(svg, map, concealState, omitHidden: true);
        }

        foreach (StairFeature stair in map.Stairs)
        {
            if (!concealState.IsEmpty && concealState.IsHidden(stair.Id))
            {
                continue;
            }

            string steps = PolylinePathData(stair.Geometry);
            if (steps.Length > 0)
            {
                AppendStroke(svg, steps, ObjectStroke, ObjectStrokeWidth);
            }
        }

        foreach (DoorFeature door in map.Doors)
        {
            if (hiddenSecrets.Contains(door.Id))
            {
                continue; // a still-secret door is wall until the DM discovers it
            }

            AppendDoor(svg, door,
                open: doorState.OpenDoorIds.Contains(door.Id),
                dbl: doorState.DoubleDoorIds.Contains(door.Id));
        }

        svg.Append("</g>");

        foreach (FeatureMarker marker in markers)
        {
            AppendMarkerGlyph(svg, marker, c, selected: false);
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    // The bounding box of the drawn cells, grown by margin and clamped to the padded map, so the
    // dilation filters run over only the revealed neighbourhood rather than the whole map.
    private static (double X, double Y, double W, double H) FilterRegion(
        IReadOnlyList<MapBounds> cells, double margin, double minX, double minY, double width, double height)
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (MapBounds b in cells.Where(IsFinite))
        {
            x0 = Math.Min(x0, b.MinX);
            y0 = Math.Min(y0, b.MinY);
            x1 = Math.Max(x1, b.MaxX);
            y1 = Math.Max(y1, b.MaxY);
        }

        if (x0 > x1)
        {
            return (minX, minY, width, height);
        }

        return (x0 - margin, y0 - margin, (x1 - x0) + (2 * margin), (y1 - y0) + (2 * margin));
    }

    // Footprints of the OPEN doors as rect subpaths — painted black into a wall mask so the wall opens
    // into a doorway there. Shared by the DM view and the player reveal. A still-hidden secret door can
    // never be open, so it is excluded and its wall stays solid.
    private static string OpenDoorHoles(VectorMap map, DoorRenderState doors, double wallW)
    {
        if (doors.OpenDoorIds.Count == 0)
        {
            return string.Empty;
        }

        double inf = Math.Max(wallW, 1);
        var rects = map.Doors
            .Where(d => doors.OpenDoorIds.Contains(d.Id) && !doors.IsHiddenSecret(d.Id) && IsFinite(d.Bounds))
            .Select(d => DoorHole(d, inf))
            .ToList();
        return RectsPathData(rects);
    }

    // A door's wall-hole: the opening spans the door bounds ALONG the wall (no growth there, so the wall
    // on either side of the opening is untouched) and is inflated only ACROSS the wall (perpendicular) so
    // it fully clears the wall stroke's thickness.
    private static MapBounds DoorHole(DoorFeature door, double inflate)
    {
        MapBounds b = door.Bounds;
        bool vertical = door.Orientation == DoorOrientation.Vertical
            || (door.Orientation == DoorOrientation.None && b.Height >= b.Width);
        double ix = vertical ? inflate : 0;
        double iy = vertical ? 0 : inflate;
        return new MapBounds(b.MinX - ix, b.MinY - iy, b.MaxX + ix, b.MaxY + iy);
    }

    // Emits the revealed polygons + cells with the given presentation attributes (fill or stroke),
    // skipping non-finite geometry so a malformed reveal fails closed (stays fogged).
    private static void AppendRevealShapes(StringBuilder svg, FogOverlay fog, string attributes)
    {
        foreach (IReadOnlyList<MapPoint> polygon in fog.RevealedPolygons)
        {
            if (polygon.Count >= 3 && AllFinite(polygon))
            {
                svg.Append("<polygon points=\"").Append(PointsAttribute(polygon)).Append("\" ")
                    .Append(attributes).Append("/>");
            }
        }

        foreach (MapBounds cell in fog.RevealedCells.Where(IsFinite))
        {
            AppendRect(svg, cell, attributes);
        }
    }

    private static void AppendMaskOpen(
        StringBuilder svg, string id, double minX, double minY, double width, double height, bool whiteBase)
    {
        svg.Append("<mask id=\"").Append(id).Append("\" maskUnits=\"userSpaceOnUse\" x=\"").Append(N(minX))
            .Append("\" y=\"").Append(N(minY)).Append("\" width=\"").Append(N(width)).Append("\" height=\"")
            .Append(N(height)).Append("\">");
        if (whiteBase)
        {
            AppendFullRect(svg, minX, minY, width, height, "white", 1, mask: "");
        }
    }

    private static void AppendFullRect(
        StringBuilder svg, double minX, double minY, double width, double height, string fill, double opacity, string mask)
    {
        svg.Append("<rect x=\"").Append(N(minX)).Append("\" y=\"").Append(N(minY)).Append("\" width=\"")
            .Append(N(width)).Append("\" height=\"").Append(N(height)).Append("\" fill=\"").Append(fill).Append('"');
        if (opacity < 1)
        {
            svg.Append(" fill-opacity=\"").Append(N(opacity)).Append('"');
        }

        if (mask.Length > 0)
        {
            svg.Append(" mask=\"url(#").Append(mask).Append(")\" pointer-events=\"none\"");
        }

        svg.Append("/>");
    }

    private static void AppendRect(StringBuilder svg, MapBounds bounds, string attributes)
    {
        svg.Append("<rect x=\"").Append(N(bounds.MinX)).Append("\" y=\"").Append(N(bounds.MinY))
            .Append("\" width=\"").Append(N(bounds.Width)).Append("\" height=\"").Append(N(bounds.Height))
            .Append("\" ").Append(attributes).Append("/>");
    }

    private static bool AllFinite(IReadOnlyList<MapPoint> points) =>
        points.All(point => double.IsFinite(point.X) && double.IsFinite(point.Y));

    private static bool IsFinite(MapBounds bounds) =>
        double.IsFinite(bounds.MinX) && double.IsFinite(bounds.MinY)
        && double.IsFinite(bounds.MaxX) && double.IsFinite(bounds.MaxY);

    // ---- Editor overlay (regions / markers / object outlines / draft) --------------------------
    // Drawn last, on top of the object pass, in the same world coordinates as the geometry. Each
    // region/marker is wrapped in an id-tagged group so the editor can hit-test and select it via
    // the same data-* hooks the object pass uses.

    private static void AppendEditorOverlay(StringBuilder svg, EditorOverlay overlay)
    {
        double unit = overlay.UnitSize > 0 ? overlay.UnitSize : 1;
        svg.Append("<g class=\"editor-overlay\">");
        AppendOverlayRegions(svg, overlay, unit);
        AppendOverlayObjects(svg, overlay, unit);
        AppendOverlayMarkers(svg, overlay, unit);
        AppendDraft(svg, overlay, unit);
        AppendPingMark(svg, overlay.Ping, unit);
        AppendMeasureLine(svg, overlay.Measure, unit);
        // Transparent object hit-targets go last so a door/stair stays clickable over a region fill.
        AppendHitTargets(svg, overlay);
        svg.Append("</g>");
    }

    // A region outline being drawn corner by corner: the outline so far, lightly filled so the shape
    // it would close reads at a glance, with the first corner drawn larger because clicking it closes
    // the outline. Click-through, so the next click on top of it still places a corner. The group
    // carries the last corner (and whether the next one snaps) for dt-editor.js, which draws the
    // segment from there to the pointer.
    private static void AppendDraft(StringBuilder svg, EditorOverlay overlay, double unit)
    {
        IReadOnlyList<MapPoint> draft = overlay.Draft;
        if (draft.Count == 0 || !AllFinite(draft))
        {
            return;
        }

        MapPoint last = draft[draft.Count - 1];
        svg.Append("<g class=\"dt-draft-poly\" pointer-events=\"none\" data-last-x=\"").Append(N(last.X))
            .Append("\" data-last-y=\"").Append(N(last.Y)).Append("\" data-snap=\"")
            .Append(overlay.DraftSnaps ? "true" : "false").Append("\">");

        if (draft.Count >= 2)
        {
            svg.Append("<polyline points=\"").Append(PointsAttribute(draft))
                .Append("\" fill=\"rgba(230,126,34,0.15)\" stroke=\"").Append(DraftColour)
                .Append("\" stroke-width=\"").Append(N(0.16 * unit)).Append("\" stroke-dasharray=\"")
                .Append(N(0.5 * unit)).Append(' ').Append(N(0.35 * unit))
                .Append("\" stroke-linejoin=\"round\"/>");
        }

        AppendDot(svg, draft[0].X, draft[0].Y, 0.34 * unit, DraftColour);
        for (int i = 1; i < draft.Count; i++)
        {
            AppendDot(svg, draft[i].X, draft[i].Y, 0.2 * unit, DraftColour);
        }

        svg.Append("</g>");
    }

    // The DM's own copy of where they last pointed: a static ring and dot, no pulse. The animation
    // is the players' cue, not the DM's — theirs only has to say "that is the spot you picked".
    private static void AppendPingMark(StringBuilder svg, PingMarker ping, double unit)
    {
        if (!ping.IsSet || !double.IsFinite(ping.X) || !double.IsFinite(ping.Y))
        {
            return;
        }

        svg.Append("<g class=\"ping-mark\" pointer-events=\"none\"><circle cx=\"").Append(N(ping.X))
            .Append("\" cy=\"").Append(N(ping.Y)).Append("\" r=\"").Append(N(1.1 * unit))
            .Append("\" fill=\"none\" stroke=\"").Append(PingColour).Append("\" stroke-width=\"")
            .Append(N(0.14 * unit)).Append("\" stroke-opacity=\"0.9\"/>");
        AppendDot(svg, ping.X, ping.Y, 0.22 * unit, PingColour);
        svg.Append("</g>");
    }

    // The measured segment: end ticks, the run between them, and the distance sitting above the
    // midpoint with a parchment halo so it reads over floor, wall band and fog alike.
    private static void AppendMeasureLine(StringBuilder svg, MapMeasurement measure, double unit)
    {
        if (!measure.IsSet
            || !double.IsFinite(measure.From.X) || !double.IsFinite(measure.From.Y)
            || !double.IsFinite(measure.To.X) || !double.IsFinite(measure.To.Y))
        {
            return;
        }

        double x0 = measure.From.X, y0 = measure.From.Y, x1 = measure.To.X, y1 = measure.To.Y;
        string run = "M" + N(x0) + ' ' + N(y0) + 'L' + N(x1) + ' ' + N(y1);

        svg.Append("<g class=\"measure-line\" pointer-events=\"none\">");

        // A dark under-stroke first, so the bright line stays readable over parchment as well as
        // over the fog tint — one colour cannot do both on this map.
        AppendStroke(svg, run, "#1b1b1b", 0.22 * unit, 0.55);
        AppendStroke(svg, run, MeasureColour, 0.1 * unit);
        AppendDot(svg, x0, y0, 0.18 * unit, MeasureColour);
        AppendDot(svg, x1, y1, 0.18 * unit, MeasureColour);

        string label = measure.ShortLabel;
        if (label.Length > 0)
        {
            double midX = (x0 + x1) / 2;
            double midY = ((y0 + y1) / 2) - (0.55 * unit);
            double fontSize = 0.9 * unit;
            svg.Append("<text x=\"").Append(N(midX)).Append("\" y=\"").Append(N(midY))
                .Append("\" font-size=\"").Append(N(fontSize))
                .Append("\" text-anchor=\"middle\" dominant-baseline=\"central\" font-family=\"sans-serif\" ")
                .Append("font-weight=\"700\" paint-order=\"stroke\" stroke=\"#f4f1ea\" stroke-width=\"")
                .Append(N(fontSize * 0.28)).Append("\" fill=\"#1b1b1b\">")
                .Append(AttributeEscape(label)).Append("</text>");
        }

        svg.Append("</g>");
    }

    private static void AppendOverlayRegions(StringBuilder svg, EditorOverlay overlay, double unit)
    {
        foreach (Region region in overlay.Regions)
        {
            if (region.Polygon.Count < 2)
            {
                continue;
            }

            bool selected = string.Equals(region.RegionId, overlay.SelectedRegionId, StringComparison.Ordinal);
            bool linked = region.GraphNodeId.Length > 0;
            string stroke = RegionStroke(selected, linked);
            string fill = RegionFill(selected, linked);
            double strokeWidth = (selected ? 0.24 : 0.13) * unit;

            svg.Append("<g data-region-id=\"").Append(AttributeEscape(region.RegionId)).Append("\">");
            svg.Append("<polygon points=\"").Append(PointsAttribute(region.Polygon))
                .Append("\" fill=\"").Append(fill).Append("\" stroke=\"").Append(stroke)
                .Append("\" stroke-width=\"").Append(N(strokeWidth)).Append("\" stroke-linejoin=\"round\"/>");

            if (selected)
            {
                foreach (MapPoint vertex in region.Polygon)
                {
                    AppendDot(svg, vertex.X, vertex.Y, 0.2 * unit, "#b8860b");
                }
            }

            if (region.Label.Length > 0)
            {
                AppendRegionLabel(svg, region.Polygon, region.Label, unit);
            }

            svg.Append("</g>");
        }
    }

    private static void AppendRegionLabel(StringBuilder svg, IReadOnlyList<MapPoint> polygon, string label, double unit)
    {
        double cx = 0;
        double cy = 0;
        foreach (MapPoint point in polygon)
        {
            cx += point.X;
            cy += point.Y;
        }

        cx /= polygon.Count;
        cy /= polygon.Count;

        // Painted with a white halo (paint-order: stroke) so the label stays legible over the map.
        svg.Append("<text x=\"").Append(N(cx)).Append("\" y=\"").Append(N(cy)).Append("\" font-size=\"")
            .Append(N(0.85 * unit)).Append("\" text-anchor=\"middle\" dominant-baseline=\"central\" ")
            .Append("font-family=\"sans-serif\" font-weight=\"600\" paint-order=\"stroke\" stroke=\"#ffffff\" stroke-width=\"")
            .Append(N(0.18 * unit)).Append("\" fill=\"#1b1b1b\" pointer-events=\"none\">")
            .Append(AttributeEscape(label)).Append("</text>");
    }

    private static void AppendOverlayObjects(StringBuilder svg, EditorOverlay overlay, double unit)
    {
        foreach (ObjectHighlight highlight in overlay.Objects)
        {
            MapBounds bounds = highlight.Bounds;
            if (bounds.Width <= 0 && bounds.Height <= 0)
            {
                continue;
            }

            // Secret and concealed both mean "the players do not see this", so they share the outline
            // style: a dashed purple box that reads differently from a plain annotated object.
            bool hiddenFromPlayers = highlight.IsSecret || highlight.IsConcealed;
            bool selected = string.Equals(highlight.Id, overlay.SelectedObjectId, StringComparison.Ordinal);
            double pad = 0.2 * unit;
            string stroke = ObjectOutlineStroke(selected, hiddenFromPlayers, highlight.Linked);
            double strokeWidth = (selected ? 0.22 : 0.14) * unit;

            svg.Append("<rect x=\"").Append(N(bounds.MinX - pad)).Append("\" y=\"").Append(N(bounds.MinY - pad))
                .Append("\" width=\"").Append(N(bounds.Width + (pad * 2))).Append("\" height=\"")
                .Append(N(bounds.Height + (pad * 2))).Append("\" rx=\"").Append(N(0.15 * unit))
                .Append("\" fill=\"none\" stroke=\"").Append(stroke).Append("\" stroke-width=\"").Append(N(strokeWidth)).Append('"');
            if (hiddenFromPlayers && !selected)
            {
                svg.Append(" stroke-dasharray=\"").Append(N(0.4 * unit)).Append(' ').Append(N(0.3 * unit)).Append('"');
            }

            svg.Append("/>");
        }
    }

    private static void AppendOverlayMarkers(StringBuilder svg, EditorOverlay overlay, double unit)
    {
        foreach (FeatureMarker marker in overlay.Markers)
        {
            bool selected = string.Equals(marker.FeatureId, overlay.SelectedFeatureId, StringComparison.Ordinal);
            AppendMarkerGlyph(svg, marker, unit, selected);
        }
    }

    private static void AppendMarkerGlyph(StringBuilder svg, FeatureMarker marker, double unit, bool selected)
    {
        (string fill, string glyph) = MarkerStyle(marker.Kind);
        double x = marker.Position.X;
        double y = marker.Position.Y;

        svg.Append("<g data-feature-id=\"").Append(AttributeEscape(marker.FeatureId)).Append("\">");
        svg.Append("<circle cx=\"").Append(N(x)).Append("\" cy=\"").Append(N(y)).Append("\" r=\"")
            .Append(N(0.42 * unit)).Append("\" fill=\"").Append(fill).Append("\" stroke=\"")
            .Append(selected ? "#b8860b" : "#1b1b1b").Append("\" stroke-width=\"")
            .Append(N((selected ? 0.18 : 0.1) * unit)).Append("\"/>");
        svg.Append("<text x=\"").Append(N(x)).Append("\" y=\"").Append(N(y)).Append("\" font-size=\"")
            .Append(N(0.55 * unit)).Append("\" fill=\"#ffffff\" text-anchor=\"middle\" dominant-baseline=\"central\" ")
            .Append("font-family=\"sans-serif\" font-weight=\"bold\">").Append(glyph).Append("</text>");
        svg.Append("</g>");
    }

    private static void AppendHitTargets(StringBuilder svg, EditorOverlay overlay)
    {
        foreach (ObjectHighlight target in overlay.HitTargets)
        {
            MapBounds bounds = target.Bounds;
            if (bounds.Width <= 0 && bounds.Height <= 0)
            {
                continue;
            }

            svg.Append("<g data-object-kind=\"").Append(ObjectKindHook(target.Kind)).Append("\" data-object-id=\"")
                .Append(AttributeEscape(target.Id)).Append("\"><rect x=\"").Append(N(bounds.MinX))
                .Append("\" y=\"").Append(N(bounds.MinY)).Append("\" width=\"").Append(N(bounds.Width))
                .Append("\" height=\"").Append(N(bounds.Height)).Append("\" fill=\"transparent\"/></g>");
        }
    }

    private static string RegionStroke(bool selected, bool linked)
    {
        if (selected)
        {
            return "#b8860b";
        }

        return linked ? "#2e8b57" : "#2f6fdb";
    }

    private static string RegionFill(bool selected, bool linked)
    {
        if (selected)
        {
            return "rgba(184,134,11,0.20)";
        }

        return linked ? "rgba(46,139,87,0.14)" : "rgba(47,111,219,0.12)";
    }

    // The data-object-kind hook the input layer reads back as a click's hit kind; it must match the
    // strings the base object pass emits ("door" / "stairs" / "decoration").
    private static string ObjectKindHook(MapObjectKind kind) => kind switch
    {
        MapObjectKind.Stairs => "stairs",
        MapObjectKind.Decoration => "decoration",
        _ => "door",
    };

    private static string ObjectOutlineStroke(bool selected, bool hiddenFromPlayers, bool linked)
    {
        if (selected)
        {
            return "#b8860b";
        }

        if (hiddenFromPlayers)
        {
            return "#8a2be2";
        }

        return linked ? "#2e8b57" : "#c07000";
    }

    private static (string Fill, string Glyph) MarkerStyle(FeatureKind kind) => kind switch
    {
        FeatureKind.Trap => ("#c0392b", "T"),
        FeatureKind.HiddenDoor => ("#8e44ad", "D"),
        FeatureKind.Secret => ("#2c3e50", "S"),
        FeatureKind.Hazard => ("#d68910", "H"),
        _ => ("#555555", "?"),
    };

    private static void AppendDot(StringBuilder svg, double x, double y, double radius, string fill)
    {
        svg.Append("<circle cx=\"").Append(N(x)).Append("\" cy=\"").Append(N(y)).Append("\" r=\"")
            .Append(N(radius)).Append("\" fill=\"").Append(fill)
            .Append("\" stroke=\"#ffffff\" stroke-width=\"").Append(N(radius * 0.25)).Append("\"/>");
    }

    private static string PointsAttribute(IReadOnlyList<MapPoint> points)
    {
        var builder = new StringBuilder(points.Count * 12);
        for (int i = 0; i < points.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(N(points[i].X)).Append(',').Append(N(points[i].Y));
        }

        return builder.ToString();
    }

    private static string N(double value) =>
        double.IsFinite(value) ? value.ToString("0.###", CultureInfo.InvariantCulture) : "0";
}
